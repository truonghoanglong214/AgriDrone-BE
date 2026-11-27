using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Health;

internal sealed class ComboAnalysisPendingImport(
    SurveyComboImportDbContext db,
    IComboAnalysisSourceContextQuery sourceQuery,
    TimeProvider timeProvider) : IComboAnalysisPendingImport
{
    private sealed record SnapshotRow(Guid TenantId, Guid FarmId, bool BoundaryValid, bool MapValid);
    private sealed record ReferenceRow(Guid Id);
    private sealed record BoundaryRow(Guid SourceId, bool Covered, double Distance);
    private sealed record SnapshotItemPair(Guid MappingCandidateId, Guid? PlantProfileId, Guid? TargetPlantProfileId);
    private sealed record ApprovalRow(Guid Id, Guid ManifestId, string ManifestHash, Guid ProcessingJobId);

    public async Task<ComboAnalysisImportOutcome> ImportAsync(
        IntegrationEventEnvelope<ComboAnalysisReadyV1> envelope, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Pending import requires the Inbox-owned transaction.");

        var p = envelope.Payload;
        var errors = ComboAnalysisReadyV1Validator.Validate(p);
        if (errors.Count != 0)
            return Reject("InvalidPayload", string.Join("; ", errors));

        if (envelope.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Reject("ContextMismatch", "Tenant context is required.");

        // ADR 0020 Rank 2: Lock Order before checking business dedup or state
        var order = await db.SurveyOrders
            .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {p.SurveyOrderId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
            return Retry("Survey order is not available yet.");

        if (order.TenantId != tenantId || order.FarmId != p.FarmId)
            return Reject("ContextMismatch", "Order does not match Tenant/Farm.");

        if (order.Scope != ContractScope.V3)
            return Reject("UnsupportedScope", "Only V3 COMBO scope is accepted.");

        var commercial = await db.SurveyOrderCommercialSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(s => s.SurveyOrderId == order.Id, cancellationToken);
        if (commercial is null)
            return Retry("Order commercial snapshot is not available yet.");

        if (commercial.ServiceCode != "COMBO" || !commercial.ScreensDisease)
            return Reject("UnsupportedService", "Order commercial policy must be COMBO with ScreensDisease enabled.");

        var fingerprint = ComboAnalysisV1Fingerprint.ForPayload(p);

        // V06 / R4: Check committed replay FIRST before checking mutable state guards
        // Dedup by Tenant + HandoffId
        var duplicateHandoff = await db.SurveyComboImportBatches.AsNoTracking()
            .SingleOrDefaultAsync(b => b.TenantId == tenantId && b.HandoffId == p.HandoffId, cancellationToken);
        if (duplicateHandoff is not null)
        {
            return duplicateHandoff.SurveyOrderId == p.SurveyOrderId && duplicateHandoff.CanonicalFingerprint == fingerprint
                ? Complete(duplicateHandoff, true, duplicateHandoff.BlockingCount)
                : Reject("IdentityPayloadConflict", "Handoff identity has different content.");
        }

        // Dedup by business identity (Tenant, Order, Attempt, Job, SourceRevision)
        var duplicateRevision = await db.SurveyComboImportBatches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId &&
                                      b.SurveyOrderId == p.SurveyOrderId &&
                                      b.CaptureAttemptId == p.CaptureAttemptId &&
                                      b.JobId == p.JobId &&
                                      b.SourceRevision == p.SourceRevision, cancellationToken);
        if (duplicateRevision is not null)
        {
            return duplicateRevision.CanonicalFingerprint == fingerprint
                ? Complete(duplicateRevision, true, duplicateRevision.BlockingCount)
                : Reject("IdentityPayloadConflict", "Processing job revision has already handed off different output.");
        }

        // Mutable state guards: Order and active attempt must be Processing for new handoff
        if (order.Status is not SurveyOrderStatus.Processing)
            return Reject("ResultLocked", $"Order is in status {order.Status} and not accepting pending analysis.");

        // V06/V11: Lock active Capture Attempt using uq_capture_attempts_active_order filter
        var attempt = await db.CaptureAttempts
            .FromSqlInterpolated($"SELECT *, xmin FROM survey.capture_attempts WHERE order_id = {order.Id} AND tenant_id = {tenantId} AND farm_id = {p.FarmId} AND status IN ('PLANNED', 'PREFLIGHT_READY', 'IN_FLIGHT', 'AWAITING_UPLOAD', 'UPLOADED', 'PROCESSING') FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
            return Reject("NoActiveAttempt", "Survey order has no active capture attempt.");

        if (attempt.Id != p.CaptureAttemptId)
            return Reject("StaleAttempt", "Capture attempt is not the active capture attempt of this order.");

        if (attempt.Status is not CaptureAttemptStatus.Processing)
            return Reject("AttemptNotProcessing", $"Capture attempt is in status {attempt.Status} and not in processing state.");

        if (attempt.MissionId.HasValue && attempt.MissionId.Value != p.MissionId)
            return Reject("ContextMismatch", $"Capture attempt is bound to mission '{attempt.MissionId.Value}', not '{p.MissionId}'.");

        // Query source job context
        var source = await sourceQuery.GetAsync(p.MissionId, p.JobId, cancellationToken);
        if (source is null || !source.Completed)
            return Retry("Completed source processing run is not available yet.");

        if (source.TenantId != tenantId || source.FarmId != p.FarmId || source.SurveyOrderId != order.Id || source.CaptureAttemptId != p.CaptureAttemptId || !source.IsComboEligible)
            return Reject("ContextMismatch", "Mission/job context mismatch with order/attempt.");

        if (source.FarmBoundaryVersionId != p.FarmBoundaryVersionId || source.FarmBaseMapVersionId != p.FarmBaseMapVersionId)
            return Reject("MapVersionMismatch", "Payload differs from capture map snapshot.");

        if (source.SourceRevision != p.SourceRevision)
            return Reject("SourceVersionConflict", "Source revision differs from payload.");

        if (p.InputApprovalId != source.InputApprovalId || p.InputManifestId != source.InputManifestId || p.InputManifestHash != source.InputManifestHash)
            return Reject("ContextMismatch", "Input approval/manifest reference differs from source context.");

        if (p.MappingCandidateBatchId != source.MappingCandidateBatchId || p.BaselineMapVersionId != source.BaselineMapVersionId)
            return Reject("ContextMismatch", "Mapping candidate batch or baseline map reference differs from source context.");

        if (!source.ApprovedJobReference.HasValue)
            return Reject("ApprovedJobReferenceMissing", "Source processing run is missing approved job reference.");

        if (string.IsNullOrWhiteSpace(attempt.ProcessingJobReference) ||
            !Guid.TryParse(attempt.ProcessingJobReference, out var approvedRef) ||
            approvedRef != source.ApprovedJobReference.Value)
        {
            return Reject("ApprovedJobReferenceMismatch", "Source approved job reference does not match capture attempt processing job reference.");
        }

        // M2: Enforce strict input approval and manifest binding
        if (!p.InputApprovalId.HasValue || p.InputApprovalId.Value == Guid.Empty ||
            !p.InputManifestId.HasValue || p.InputManifestId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(p.InputManifestHash))
        {
            return Reject("ApprovalRequired", "COMBO analysis requires verified InputApprovalId, InputManifestId, and InputManifestHash.");
        }

        var dbApproval = await db.Database.SqlQuery<ApprovalRow>($"""
            SELECT id AS "Id", input_manifest_id AS "ManifestId", manifest_fingerprint AS "ManifestHash", processing_job_id AS "ProcessingJobId"
            FROM survey.capture_input_approvals
            WHERE id = {p.InputApprovalId.Value}
              AND capture_attempt_id = {attempt.Id}
              AND tenant_id = {tenantId}
              AND farm_id = {p.FarmId}
              AND survey_order_id = {order.Id}
              AND mission_id = {p.MissionId}
            """).SingleOrDefaultAsync(cancellationToken);

        if (dbApproval is null)
        {
            return Reject("ApprovalNotFound", "Approved capture input record not found for this attempt/mission context.");
        }

        if (string.IsNullOrWhiteSpace(attempt.InputApprovalReference) ||
            !Guid.TryParse(attempt.InputApprovalReference, out var attemptApprovalRef) ||
            attemptApprovalRef != dbApproval.Id)
        {
            return Reject("ApprovalReferenceMismatch", "Capture attempt InputApprovalReference does not match the approved input.");
        }

        if (dbApproval.ManifestId != p.InputManifestId.Value ||
            !string.Equals(dbApproval.ManifestHash, p.InputManifestHash, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("ApprovalVerificationFailed", "Input approval reference or manifest fingerprint does not match approved capture records.");
        }

        if (dbApproval.ProcessingJobId != source.ApprovedJobReference.Value)
        {
            return Reject("ProcessingJobMismatch", "Approval ProcessingJobId does not match source ApprovedJobReference.");
        }

        var dbManifest = await db.CaptureInputManifests.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == dbApproval.ManifestId &&
                                       m.CaptureAttemptId == attempt.Id &&
                                       m.TenantId == tenantId &&
                                       m.FarmId == p.FarmId &&
                                       m.SurveyOrderId == order.Id &&
                                       m.MissionId == p.MissionId, cancellationToken);

        if (dbManifest is null)
        {
            return Reject("ManifestNotFound", "Approved capture input manifest record not found.");
        }

        if (dbManifest.Status != CaptureInputManifestStatus.Approved)
        {
            return Reject("ManifestNotApproved", "Capture input manifest is not in Approved status.");
        }

        if (!string.Equals(dbManifest.ManifestFingerprint, p.InputManifestHash, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("ManifestFingerprintMismatch", "Approved manifest fingerprint does not match payload fingerprint.");
        }

        var frozenManifestFiles = await db.CaptureInputManifestFiles.AsNoTracking()
            .Where(f => f.ManifestId == dbManifest.Id && (f.MediaRole == "RawImage" || f.MediaRole == "RAW_IMAGE"))
            .Select(f => f.MediaAssetId)
            .ToListAsync(cancellationToken);

        if (frozenManifestFiles.Count == 0)
        {
            return Reject("FrozenFilesMissing", "Approved capture manifest has no frozen raw images.");
        }

        if (source.InputMediaIds is not { Count: > 0 } inputIds)
        {
            return Reject("InputMediaMissing", "Source processing run must have a non-empty set of input media.");
        }

        if (inputIds.Distinct().Count() != inputIds.Count)
        {
            return Reject("DuplicateInputMedia", "Source input media contains duplicate IDs.");
        }

        var inputSet = inputIds.ToHashSet();
        var frozenSet = frozenManifestFiles.ToHashSet();
        if (!inputSet.SetEquals(frozenSet))
        {
            return Reject("InputMediaMismatch", "Source input media does not match the exact set of approved manifest raw images.");
        }

        // V07: Evidence media validation across findings, boundary exceptions, and disease zones
        var evidenceIds = p.Findings.SelectMany(f => f.EvidenceMedia)
            .Concat(p.BoundaryExceptions.SelectMany(e => e.EvidenceMedia))
            .Concat(p.DiseaseZones.SelectMany(z => z.EvidenceMedia))
            .Select(m => m.MediaAssetId).Distinct().ToHashSet();
        if (!evidenceIds.IsSubsetOf(source.EvidenceMediaIds))
            return Reject("ContextMismatch", "Evidence media does not belong to the source Mission/Tenant/Farm.");

        // Map validity predicate: status IN ('INTERNALLY_APPROVED', 'PUBLISHED')
        var snapshot = await db.Database.SqlQuery<SnapshotRow>($"""
            SELECT f.tenant_id AS "TenantId", f.id AS "FarmId",
                (fb.reviewed_at IS NOT NULL AND fb.status IN ('APPROVED', 'SUPERSEDED')) AS "BoundaryValid",
                COALESCE(bm.status IN ('INTERNALLY_APPROVED', 'PUBLISHED') AND bm.farm_boundary_version_id = fb.id, FALSE) AS "MapValid"
            FROM farm.farms f
            JOIN farm.farm_boundaries fb ON fb.farm_id = f.id AND fb.tenant_id = f.tenant_id AND fb.id = {p.FarmBoundaryVersionId}
            JOIN farm.farm_base_map_versions bm ON bm.farm_id = f.id AND bm.tenant_id = f.tenant_id AND bm.id = {p.FarmBaseMapVersionId}
            WHERE f.id = {p.FarmId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null)
            return Retry("Farm/map snapshot is not available yet.");

        if (snapshot.TenantId != tenantId || !snapshot.BoundaryValid || !snapshot.MapValid)
            return Reject("MapVersionMismatch", "An approved boundary and internally approved or published capture map are required.");

        // Candidate verification
        var candidates = p.Findings.Where(f => f.MappingCandidateId.HasValue).Select(f => f.MappingCandidateId!.Value).Distinct().ToArray();
        if (candidates.Length > 0)
        {
            var validCandidates = await db.Database.SqlQuery<ReferenceRow>($"""
                SELECT c.candidate_id AS "Id" FROM farm.mapping_candidates c
                JOIN farm.mapping_candidate_batches b ON b.id = c.batch_id
                WHERE c.candidate_id = ANY({candidates}) AND b.farm_id = {p.FarmId} AND b.tenant_id = {tenantId}
                  AND b.id = {p.MappingCandidateBatchId}
                """).Distinct().ToListAsync(cancellationToken);
            if (validCandidates.Count != candidates.Length)
                return Reject("ContextMismatch", "Mapping candidate reference does not match the candidate batch.");
        }

        // Plant profile verification
        var plantProfiles = p.Findings.Where(f => f.PlantProfileId.HasValue).Select(f => f.PlantProfileId!.Value).Distinct().ToArray();
        if (plantProfiles.Length > 0)
        {
            var validPlants = await db.Database.SqlQuery<ReferenceRow>($"""
                SELECT p.plant_profile_id AS "Id" FROM plant.plants p
                WHERE p.plant_profile_id = ANY({plantProfiles}) AND p.farm_id = {p.FarmId}
                """).Distinct().ToListAsync(cancellationToken);
            if (validPlants.Count != plantProfiles.Length)
                return Reject("ContextMismatch", "Plant profile reference does not belong to the capture Farm.");
        }

        // V07: Candidate-profile pairing verification via SurveyOrderPoleCountSnapshotItem
        var snapshotPairs = await db.Database.SqlQuery<SnapshotItemPair>($"""
            SELECT mapping_candidate_id AS "MappingCandidateId",
                   plant_profile_id AS "PlantProfileId",
                   target_plant_profile_id AS "TargetPlantProfileId"
            FROM survey.survey_order_pole_count_snapshot_items
            WHERE mapping_candidate_batch_id = {p.MappingCandidateBatchId}
              AND tenant_id = {tenantId} AND farm_id = {p.FarmId}
            """).ToListAsync(cancellationToken);

        var pairLookup = snapshotPairs.ToDictionary(
            item => item.MappingCandidateId,
            item => item.PlantProfileId ?? item.TargetPlantProfileId);

        foreach (var f in p.Findings)
        {
            if (f.MappingCandidateId.HasValue && f.PlantProfileId.HasValue)
            {
                if (!pairLookup.TryGetValue(f.MappingCandidateId.Value, out var expectedProfile) || !expectedProfile.HasValue)
                {
                    return Reject("ContextMismatch", $"No approved mapping evidence found for finding candidate {f.MappingCandidateId.Value} with profile {f.PlantProfileId.Value}.");
                }

                if (expectedProfile.Value != f.PlantProfileId.Value)
                {
                    return Reject("ContextMismatch", $"Finding candidate {f.MappingCandidateId.Value} is mapped to profile {expectedProfile.Value} in pole count snapshot, not {f.PlantProfileId.Value}.");
                }
            }
        }

        // M3 / V07: Disease zones geometry and membership validation with NTS topology and covers check
        var findingsById = p.Findings.ToDictionary(f => f.FindingId);
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        foreach (var zone in p.DiseaseZones)
        {
            var ringCoords = zone.Geometry.Coordinates.Count == 0 ? null : zone.Geometry.Coordinates[0];
            if (ringCoords == null || ringCoords.Count < 4)
                return Reject("InvalidGeometry", $"Zone {zone.SourceProposalId} polygon has fewer than 4 coordinates.");

            var first = ringCoords[0];
            var last = ringCoords[^1];
            if (first.Count < 2 || last.Count < 2 || first[0] != last[0] || first[1] != last[1])
                return Reject("InvalidGeometry", $"Zone {zone.SourceProposalId} polygon ring is not closed.");

            LinearRing shell;
            Polygon zonePolygon;
            try
            {
                var ntsCoords = ringCoords.Select(c => new Coordinate(c[0], c[1])).ToArray();
                shell = geometryFactory.CreateLinearRing(ntsCoords);
                zonePolygon = geometryFactory.CreatePolygon(shell);
            }
            catch (Exception ex)
            {
                return Reject("InvalidGeometry", $"Zone {zone.SourceProposalId} polygon geometry is malformed: {ex.Message}");
            }

            if (!zonePolygon.IsValid)
            {
                return Reject("InvalidGeometry", $"Zone {zone.SourceProposalId} polygon is not a valid simple polygon (self-intersecting or invalid topology).");
            }

            foreach (var m in zone.Memberships)
            {
                if (!findingsById.TryGetValue(m.FindingId, out var finding))
                    return Reject("ContextMismatch", $"Zone {zone.SourceProposalId} membership finding {m.FindingId} is not present in batch findings.");

                if (m.PlantProfileId.HasValue && finding.PlantProfileId != m.PlantProfileId)
                    return Reject("ContextMismatch", $"Zone {zone.SourceProposalId} membership profile {m.PlantProfileId} does not match finding profile {finding.PlantProfileId}.");

                if (m.MappingCandidateId.HasValue && finding.MappingCandidateId != m.MappingCandidateId)
                    return Reject("ContextMismatch", $"Zone {zone.SourceProposalId} membership candidate {m.MappingCandidateId} does not match finding candidate {finding.MappingCandidateId}.");

                var findingPoint = geometryFactory.CreatePoint(new Coordinate(finding.Position.Coordinates[0], finding.Position.Coordinates[1]));
                if (!zonePolygon.Covers(findingPoint))
                {
                    return Reject("ZoneGeometryMismatch", $"Zone {zone.SourceProposalId} geometry does not cover member finding {m.FindingId} at ({findingPoint.X}, {findingPoint.Y}).");
                }
            }
        }

        // Recalculate boundary distances and covers via PostGIS
        var positions = JsonSerializer.Serialize(p.Findings.Select(f => new
        {
            SourceId = f.FindingId,
            Longitude = f.Position.Coordinates[0],
            Latitude = f.Position.Coordinates[1]
        }));

        Dictionary<Guid, BoundaryRow> boundaryRows;
        if (p.Findings.Count > 0)
        {
            boundaryRows = await db.Database.SqlQuery<BoundaryRow>($"""
                SELECT i."SourceId", ST_Covers(b.geometry, ST_SetSRID(ST_MakePoint(i."Longitude", i."Latitude"), 4326)) AS "Covered",
                  ST_Distance(ST_Boundary(b.geometry)::geography, ST_SetSRID(ST_MakePoint(i."Longitude", i."Latitude"), 4326)::geography) AS "Distance"
                FROM farm.farm_boundaries b CROSS JOIN jsonb_to_recordset({positions}::jsonb)
                  AS i("SourceId" uuid, "Longitude" double precision, "Latitude" double precision)
                WHERE b.id = {p.FarmBoundaryVersionId}
                """).ToDictionaryAsync(r => r.SourceId, cancellationToken);

            if (boundaryRows.Count != p.Findings.Count)
                return Reject("SpatialCalculationFailure", "Failed to compute boundary spatial metrics for all findings.");
        }
        else
        {
            boundaryRows = [];
        }

        // M4 / V07 / V11: Persist stable structured blocking items decided by import
        List<ComboPendingBlockingItem> blockingItems = [];
        foreach (var e in p.BoundaryExceptions)
        {
            blockingItems.Add(new(
                SourceType: "BOUNDARY_EXCEPTION",
                SourceId: e.SourceReferenceId,
                ReasonCode: e.BoundaryClassification,
                Description: $"Boundary exception measured {e.MeasuredBoundaryDistanceM:F2}m (threshold: {e.BoundaryThresholdM:F2}m)."));
        }

        foreach (var f in p.Findings)
        {
            if (boundaryRows.TryGetValue(f.FindingId, out var row))
            {
                var isNearOrOut = !row.Covered || row.Distance <= (double)ComboAnalysisV1Values.BoundaryReviewThresholdMeters;
                if (isNearOrOut && !p.BoundaryExceptions.Any(e => e.SourceReferenceId == f.FindingId.ToString("D")))
                {
                    blockingItems.Add(new(
                        SourceType: "FINDING_SPATIAL",
                        SourceId: f.FindingId.ToString("D"),
                        ReasonCode: row.Covered ? "NEEDS_REVIEW" : "OUT_OF_BOUNDARY",
                        Description: $"PostGIS verified finding distance {row.Distance:F2}m (covered: {row.Covered})."));
                }
            }

            if (f.Uncertainty == "REQUIRES_REVIEW")
            {
                blockingItems.Add(new(
                    SourceType: "FINDING_UNCERTAINTY",
                    SourceId: f.FindingId.ToString("D"),
                    ReasonCode: f.Uncertainty,
                    Description: "Finding uncertainty marked as REQUIRES_REVIEW."));
            }
            else if (!f.PlantProfileId.HasValue && f.MappingCandidateId.HasValue)
            {
                blockingItems.Add(new(
                    SourceType: "FINDING_IDENTITY",
                    SourceId: f.FindingId.ToString("D"),
                    ReasonCode: "CandidateOnlyUnresolved",
                    Description: $"Candidate {f.MappingCandidateId.Value} is not mapped to an active plant profile."));
            }
            else if (!f.PlantProfileId.HasValue && !f.MappingCandidateId.HasValue)
            {
                blockingItems.Add(new(
                    SourceType: "FINDING_IDENTITY",
                    SourceId: f.FindingId.ToString("D"),
                    ReasonCode: "UnresolvedIdentity",
                    Description: "Finding has neither plant profile nor mapping candidate reference."));
            }
        }

        int blockingCount = blockingItems.Count;
        var blockingItemsDoc = JsonSerializer.SerializeToDocument(blockingItems);

        // Lineage resolution under Order lock
        Guid? predecessorBatchId = null;
        if (p.SupersedesHandoffId.HasValue)
        {
            var predecessorBatch = await db.SurveyComboImportBatches
                .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_combo_import_batches WHERE tenant_id = {tenantId} AND handoff_id = {p.SupersedesHandoffId.Value} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (predecessorBatch is null)
            {
                return Retry("Predecessor handoff batch is not available yet.");
            }

            if (predecessorBatch.SurveyOrderId != p.SurveyOrderId ||
                predecessorBatch.FarmId != p.FarmId ||
                predecessorBatch.CaptureAttemptId != p.CaptureAttemptId)
            {
                return Reject("PredecessorContextMismatch", "Predecessor batch does not match order/farm/attempt context.");
            }

            if (p.SourceRevision <= predecessorBatch.SourceRevision)
            {
                return Reject("SourceRevisionStale", "Source revision must be strictly greater than predecessor revision.");
            }

            var existingSuccessor = await db.SurveyComboImportBatches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.PredecessorBatchId == predecessorBatch.Id, cancellationToken);
            if (existingSuccessor is not null && existingSuccessor.HandoffId != p.HandoffId)
            {
                return Reject("LineageForkConflict", "A successor batch already exists for this predecessor.");
            }

            predecessorBatchId = predecessorBatch.Id;
        }
        else
        {
            if (p.SourceRevision != 1)
            {
                return Reject("InvalidInitialRevision", "Initial batch without predecessor must have SourceRevision = 1.");
            }

            var existingRoot = await db.SurveyComboImportBatches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId &&
                                          b.SurveyOrderId == p.SurveyOrderId &&
                                          b.CaptureAttemptId == p.CaptureAttemptId &&
                                          b.PredecessorBatchId == null, cancellationToken);
            if (existingRoot is not null && existingRoot.HandoffId != p.HandoffId)
            {
                return Reject("RootBatchConflict", "A root batch already exists for this attempt.");
            }
        }

        var batchResult = SurveyComboImportBatch.Create(
            id: Guid.NewGuid(),
            tenantId: tenantId,
            farmId: p.FarmId,
            surveyOrderId: p.SurveyOrderId,
            captureAttemptId: p.CaptureAttemptId,
            missionId: p.MissionId,
            jobId: p.JobId,
            sourceRevision: p.SourceRevision,
            handoffId: p.HandoffId,
            businessPolicyVersionId: p.BusinessPolicyVersionId,
            mappingKind: p.MappingKind.ToString(),
            mappingCandidateBatchId: p.MappingCandidateBatchId,
            farmBaseMapVersionId: p.FarmBaseMapVersionId,
            baselineMapVersionId: p.BaselineMapVersionId,
            farmBoundaryVersionId: p.FarmBoundaryVersionId,
            rawPayload: JsonSerializer.SerializeToDocument(p),
            canonicalFingerprint: fingerprint,
            producedAt: p.ProducedAt,
            importedAt: timeProvider.GetUtcNow(),
            blockingCount: blockingCount,
            blockingItems: blockingItemsDoc,
            predecessorBatchId: predecessorBatchId,
            supersedesHandoffId: p.SupersedesHandoffId,
            inputApprovalId: p.InputApprovalId,
            inputManifestId: p.InputManifestId,
            inputManifestHash: p.InputManifestHash);

        if (batchResult.IsFailure)
            return Reject("InvalidBatch", batchResult.Error.Description);

        var batch = batchResult.Value;
        db.SurveyComboImportBatches.Add(batch);

        var auditData = JsonSerializer.SerializeToDocument(new
        {
            batch.Id,
            batch.SurveyOrderId,
            batch.CaptureAttemptId,
            batch.HandoffId,
            batch.JobId,
            batch.SourceRevision,
            batch.PredecessorBatchId,
            batch.SupersedesHandoffId,
            batch.BlockingCount,
            BlockingItems = blockingItems
        });

        db.AddAuditLog(AuditLog.ForIntegrationAction(
            tenantId: tenantId,
            farmId: p.FarmId,
            sourceJobId: p.JobId,
            correlationId: envelope.CorrelationId,
            entityType: "SurveyComboImportBatch",
            entityId: batch.Id,
            action: "IMPORTED",
            newData: auditData,
            createdAt: timeProvider.GetUtcNow()));

        return Complete(batch, false, blockingCount);
    }

    private static ComboAnalysisImportOutcome Complete(SurveyComboImportBatch batch, bool idempotent, int blockingCount) =>
        new(ComboAnalysisImportDisposition.Completed, batch.Id, idempotent, blockingCount);

    private static ComboAnalysisImportOutcome Retry(string error) =>
        new(ComboAnalysisImportDisposition.Retry, Error: error);

    private static ComboAnalysisImportOutcome Reject(string errorCode, string error) =>
        new(ComboAnalysisImportDisposition.Rejected, ErrorCode: errorCode, Error: error);
}
