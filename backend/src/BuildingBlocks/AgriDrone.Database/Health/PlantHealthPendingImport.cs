using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Plants.Domain.Conditions;
using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Domain.Guidance;
using AgriDrone.Modules.Plants.Domain.Health;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthPendingImport(
    SurveyResultPublicationDbContext db,
    IPlantHealthSourceContextQuery sourceQuery,
    TimeProvider timeProvider) : IPlantHealthPendingImport
{
    private sealed record SnapshotRow(Guid TenantId, Guid FarmId, bool BoundaryValid, bool MapValid);
    private sealed record ReferenceRow(Guid Id);
    private sealed record BoundaryRow(Guid SourceId, bool Covered, double Distance);
    private sealed record PreparedFinding(PlantHealthFindingV4 Source, string? ClassCode, string Boundary);
    private sealed record PreparedZone(ProposedDiseaseZoneV4 Source, Guid ConditionId, Polygon Geometry, bool Blocking);

    public async Task<PlantHealthImportOutcome> ImportAsync(
        IntegrationEventEnvelope<PlantHealthAnalysisReadyV4> envelope, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Pending import requires the Inbox-owned transaction.");
        var p = envelope.Payload;
        var errors = V4BusinessContractValidators.Validate(p);
        if (errors.Count != 0) return Reject("InvalidPayload", string.Join("; ", errors));
        if (envelope.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Reject("ContextMismatch", "Tenant context is required.");

        // Serialize deliveries for the same Order before checking business dedup/state.
        var order = await db.SurveyOrders.FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {p.SurveyOrderId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null) return Retry("Survey order is not available yet.");
        if (order.TenantId != tenantId || order.FarmId != p.FarmId)
            return Reject("ContextMismatch", "Order does not match Tenant/Farm.");
        var fingerprint = PlantHealthV4Fingerprint.ForPayload(p);
        var duplicate = await db.HealthImportBatches.AsNoTracking().SingleOrDefaultAsync(
            b => b.TenantId == tenantId && b.HandoffId == p.HandoffId, cancellationToken);
        if (duplicate is not null)
            return duplicate.SurveyOrderId == p.SurveyOrderId && duplicate.Fingerprint == fingerprint
                ? Complete(duplicate, true) : Reject("IdentityPayloadConflict", "Handoff identity has different content.");
        var sameJob = await db.HealthImportBatches.AsNoTracking().FirstOrDefaultAsync(
            b => b.TenantId == tenantId && b.MissionId == p.MissionId && b.JobId == p.JobId, cancellationToken);
        if (sameJob is not null)
            return sameJob.Fingerprint == fingerprint ? Complete(sameJob, true)
                : Reject("IdentityPayloadConflict", "Processing job has already handed off different output.");
        var isPlantHealth = await db.Database.SqlQuery<bool>($"SELECT service_type = 'PLANT_HEALTH' AS \"Value\" FROM survey.survey_services WHERE id = {order.SurveyServiceId}")
            .SingleAsync(cancellationToken);
        if (!isPlantHealth)
            return Reject("UnsupportedService", "Only Plant Health output is accepted.");
        if (order.Status is not (SurveyOrderStatus.InProgress or SurveyOrderStatus.PendingReview))
            return Reject("ResultLocked", "Order is not accepting pending analysis.");
        var result = await db.SurveyResults.FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_results WHERE survey_order_id = {order.Id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (result is not null && result.Status != SurveyResultStatus.PendingReview)
            return Reject("ResultLocked", "Approved/published results cannot be reprocessed by ingestion.");
        var source = await sourceQuery.GetAsync(p.MissionId, p.JobId, cancellationToken);
        if (source is null || !source.Completed) return Retry("Completed source processing run is not available yet.");
        if (source.TenantId != tenantId || source.FarmId != p.FarmId || source.SurveyOrderId != order.Id || !source.IsPlantHealth)
            return Reject("ContextMismatch", "Mission/job does not match this Plant Health Order.");
        if (source.FarmBoundaryVersionId != p.FarmBoundaryVersionId || source.FarmBaseMapVersionId != p.FarmBaseMapVersionId)
            return Reject("MapVersionMismatch", "Payload differs from the capture/job map snapshot.");
        if (source.SourceVersion <= 0 || source.Provenance != p.Provenance || source.TeamLabelLimitation != p.TeamLabelLimitation ||
            p.Findings.Any(f => f.FindingVersion != source.SourceVersion))
            return Reject("SourceVersionConflict", "Finding revision/provenance differs from the source processing run.");
        var evidenceIds = p.Findings.SelectMany(f => f.EvidenceMedia).Concat(p.BoundaryExceptions.SelectMany(e => e.EvidenceMedia))
            .Concat(p.DiseaseZones.SelectMany(z => z.EvidenceMedia)).Select(m => m.MediaAssetId).Distinct().ToHashSet();
        if (!evidenceIds.IsSubsetOf(source.EvidenceMediaIds))
            return Reject("ContextMismatch", "Evidence media does not belong to the source Mission/Tenant/Farm.");
        var snapshot = await db.Database.SqlQuery<SnapshotRow>($"""
            SELECT f.tenant_id AS "TenantId", f.id AS "FarmId",
                (fb.reviewed_at IS NOT NULL AND fb.status IN ('APPROVED', 'SUPERSEDED')) AS "BoundaryValid",
                COALESCE(bm.published_at IS NOT NULL AND bm.farm_boundary_version_id = fb.id, FALSE) AS "MapValid"
            FROM farm.farms f
            JOIN farm.farm_boundaries fb ON fb.farm_id = f.id AND fb.tenant_id = f.tenant_id AND fb.id = {p.FarmBoundaryVersionId}
            JOIN farm.farm_base_map_versions bm ON bm.farm_id = f.id AND bm.tenant_id = f.tenant_id AND bm.id = {p.FarmBaseMapVersionId}
            WHERE f.id = {p.FarmId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null) return Retry("Farm/map snapshot is not available yet.");
        if (snapshot.TenantId != tenantId || !snapshot.BoundaryValid || !snapshot.MapValid)
            return Reject("MapVersionMismatch", "An approved boundary and published capture map are required.");

        var plants = p.Findings.Select(f => f.PlantId).Concat(p.DiseaseZones.SelectMany(z => z.Memberships.Select(m => m.PlantId)))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var validPlants = await db.Database.SqlQuery<ReferenceRow>($"""
            SELECT p.id AS "Id" FROM plant.plants p
            WHERE p.id = ANY({plants}) AND p.farm_id = {p.FarmId} AND p.farm_base_map_version_id = {p.FarmBaseMapVersionId}
            """).ToListAsync(cancellationToken);
        if (validPlants.Count != plants.Length)
            return Reject("ContextMismatch", "Plant reference does not belong to the capture Farm/map snapshot.");
        var candidates = p.Findings.Select(f => f.MappingCandidateId).Concat(p.DiseaseZones.SelectMany(z => z.Memberships.Select(m => m.MappingCandidateId)))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var validCandidates = await db.Database.SqlQuery<ReferenceRow>($"""
            SELECT c.candidate_id AS "Id" FROM farm.mapping_candidates c
            JOIN farm.mapping_candidate_batches b ON b.id = c.batch_id
            WHERE c.candidate_id = ANY({candidates}) AND b.farm_id = {p.FarmId} AND b.tenant_id = {tenantId}
              AND b.farm_boundary_version_id = {p.FarmBoundaryVersionId}
              AND b.farm_base_map_version_id = {p.FarmBaseMapVersionId}
            """).Distinct().ToListAsync(cancellationToken);
        if (validCandidates.Count != candidates.Length)
            return Reject("ContextMismatch", "Mapping candidate reference has a different Farm/boundary context.");

        var catalogue = await db.PlantConditions.AsNoTracking().Where(c => c.IsActive && c.PlantHealthLabelKind != null).ToListAsync(cancellationToken);
        var diseaseClasses = catalogue.Where(c => c.PlantHealthLabelKind == PlantHealthLabelKind.SuspectedDisease).ToDictionary(c => c.Code, StringComparer.Ordinal);
        if (diseaseClasses.Count is < 2 or > 3) return Reject("InvalidCatalogue", "Configure two or three active suspected classes.");
        if (catalogue.Any(c => c.DatasetVersion != p.Provenance.DatasetVersion || c.AnnotationRuleVersion != p.Provenance.AnnotationRuleVersion))
            return Reject("SourceVersionConflict", "Dataset/annotation provenance differs from the configured team-labelled catalogue.");

        var preparedFindings = new List<PreparedFinding>();
        var positions = JsonSerializer.Serialize(p.Findings.Select(f => new
        { SourceId = f.FindingId, Longitude = f.Position.Coordinates[0], Latitude = f.Position.Coordinates[1] }));
        var boundaryRows = await db.Database.SqlQuery<BoundaryRow>($"""
            SELECT i."SourceId", ST_Covers(b.geometry, ST_SetSRID(ST_MakePoint(i."Longitude", i."Latitude"), 4326)) AS "Covered",
              ST_Distance(ST_Boundary(b.geometry)::geography, ST_SetSRID(ST_MakePoint(i."Longitude", i."Latitude"), 4326)::geography) AS "Distance"
            FROM farm.farm_boundaries b CROSS JOIN jsonb_to_recordset({positions}::jsonb)
              AS i("SourceId" uuid, "Longitude" double precision, "Latitude" double precision)
            WHERE b.id = {p.FarmBoundaryVersionId}
            """).ToDictionaryAsync(r => r.SourceId, cancellationToken);
        foreach (var finding in p.Findings)
        {
            var row = boundaryRows[finding.FindingId];
            var classification = !row.Covered ? "OUT_OF_BOUNDARY" : row.Distance <= (double)PlantHealthV4Values.BoundaryReviewThresholdMeters ? "NEEDS_REVIEW" : "IN_BOUNDARY";
            preparedFindings.Add(new(finding, diseaseClasses.ContainsKey(finding.SuspectedClassCode) ? finding.SuspectedClassCode : null, classification));
        }
        var preparedZones = new List<PreparedZone>();
        foreach (var zone in p.DiseaseZones)
        {
            if (!diseaseClasses.TryGetValue(zone.SuspectedClassCode, out var condition))
                return Reject("InvalidCatalogue", "Zone class must be a configured suspected disease.");
            var geometry = Polygon(zone.Geometry.Coordinates);
            if (!geometry.IsValid || geometry.IsEmpty) return Reject("InvalidGeometry", "Zone geometry is invalid.");
            var covered = await db.Database.SqlQuery<bool>($"SELECT ST_Covers(geometry, {geometry}) AS \"Value\" FROM farm.farm_boundaries WHERE id = {p.FarmBoundaryVersionId}")
                .SingleAsync(cancellationToken);
            var blocking = !covered || zone.Memberships.Any(m => !m.PlantId.HasValue) ||
                zone.Memberships.Any(m => !p.Findings.Any(f => (m.PlantId.HasValue && f.PlantId == m.PlantId ||
                    m.MappingCandidateId.HasValue && f.MappingCandidateId == m.MappingCandidateId) && f.SuspectedClassCode == zone.SuspectedClassCode));
            blocking |= zone.Memberships.Any(m => preparedFindings.Where(f =>
                    m.PlantId.HasValue && f.Source.PlantId == m.PlantId ||
                    m.MappingCandidateId.HasValue && f.Source.MappingCandidateId == m.MappingCandidateId)
                .Any(f => !geometry.Covers(new Point(f.Source.Position.Coordinates[0], f.Source.Position.Coordinates[1]) { SRID = 4326 })));
            foreach (var guidance in zone.GuidanceCandidates)
            {
                var entry = await db.SourcedGuidance.AsNoTracking().SingleOrDefaultAsync(g => g.Id == guidance.GuidanceId, cancellationToken);
                blocking |= entry is null || entry.Code != guidance.Code || entry.VersionNumber != guidance.VersionNumber ||
                    entry.SourceReference != guidance.SourceReference || entry.SuspectedClassCode != zone.SuspectedClassCode ||
                    entry.Status != SourcedGuidanceStatus.Active || !entry.IsEffectiveAt(p.ProducedAt);
            }
            preparedZones.Add(new(zone, condition.Id, geometry, blocking));
        }
        var sourceIds = p.Findings.Select(f => f.FindingId).ToArray();
        var existing = await db.HealthFindings.Where(f => f.TenantId == tenantId && f.MissionId == p.MissionId && sourceIds.Contains(f.SourceFindingId))
            .ToListAsync(cancellationToken);
        foreach (var f in p.Findings)
        {
            var old = existing.SingleOrDefault(e => e.SourceFindingId == f.FindingId && e.SourceVersion == f.FindingVersion);
            if (old is not null && (old.Fingerprint != PlantHealthV4Fingerprint.ForFinding(f) || old.SurveyResultId != result?.Id))
                return Reject("IdentityPayloadConflict", "Source finding revision has different content/context.");
        }
        var previous = result?.ActiveImportBatchId is Guid activeId
            ? await db.HealthImportBatches.SingleAsync(b => b.Id == activeId, cancellationToken) : null;
        if (previous is not null && previous.MissionId != p.MissionId)
            return Reject("ContextMismatch", "A different Mission requires an explicit result revision workflow.");
        if (previous is not null && previous.SourceVersion == source.SourceVersion)
            return Reject("SourceVersionConflict", "This result already has another output for the source version.");

        // No permanent-failure branch below: all writes now succeed or throw/rollback together.
        var now = timeProvider.GetUtcNow();
        if (result is null)
        {
            result = SurveyResult.CreateForManagerReview(order.Id, tenantId, p.FarmId,
                SurveyServiceType.PlantHealth, JsonSerializer.SerializeToDocument(p.Provenance), now);
            db.SurveyResults.Add(result);
        }
        var blockingCount = preparedFindings.Count(f => f.ClassCode is null || f.Source.PlantId is null || f.Boundary != "IN_BOUNDARY" || f.Source.Uncertainty != "NONE")
            + p.BoundaryExceptions.Count + preparedZones.Count(z => z.Blocking);
        var batch = PlantHealthImportBatch.Create(tenantId, result.Id, p, source.SourceVersion,
            previous is not null && previous.SourceVersion < source.SourceVersion ? previous.Id : null, blockingCount, now);
        db.HealthImportBatches.Add(batch);
        foreach (var f in preparedFindings)
        {
            var finding = existing.SingleOrDefault(e => e.SourceFindingId == f.Source.FindingId && e.SourceVersion == f.Source.FindingVersion);
            if (finding is null)
            {
                var superseded = existing.Where(e => e.SourceFindingId == f.Source.FindingId && e.SourceVersion < f.Source.FindingVersion)
                    .OrderByDescending(e => e.SourceVersion).FirstOrDefault();
                finding = PlantHealthFinding.Create(tenantId, p.FarmId, result.Id, p.MissionId, f.Source,
                    f.ClassCode, f.Boundary, superseded?.Id, now);
                db.HealthFindings.Add(finding);
            }
            db.HealthBatchFindings.Add(PlantHealthBatchFinding.Create(batch.Id, finding));
        }
        foreach (var exception in p.BoundaryExceptions)
            db.HealthBoundaryExceptions.Add(PlantHealthBoundaryException.Create(batch.Id, tenantId, p.FarmId, result.Id, exception));
        foreach (var zone in preparedZones)
            db.DiseaseZones.Add(DiseaseZone.CreatePendingHealthProposal(tenantId, p.FarmId, order.Id, result.Id,
                batch.Id, p.FarmBoundaryVersionId, p.FarmBaseMapVersionId, p.HandoffId, p.JobId,
                zone.ConditionId, zone.Source, zone.Geometry, zone.Blocking, now));
        if (previous is null || previous.SourceVersion < source.SourceVersion)
            result.ActivateImport(batch, p.Findings.Count == 0 ? null : p.Findings.Max(f => f.ObservedAt), p.TeamLabelLimitation, now);
        if (order.Status == SurveyOrderStatus.InProgress) order.MarkAnalysisReady(now);
        db.AddAuditLog(AuditLog.ForIntegrationAction(tenantId, p.FarmId, p.JobId, envelope.CorrelationId, "PlantHealthImportBatch", batch.Id,
            "PlantHealth.PendingImported", JsonSerializer.SerializeToDocument(new
            { tenantId, p.FarmId, p.SurveyOrderId, p.MissionId, p.JobId, p.HandoffId, batch.SourceVersion, blockingCount }), now));
        return Complete(batch, false);
    }

    private static Polygon Polygon(IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> rings)
    {
        var lines = rings.Select(r => new LinearRing(r.Select(c => new Coordinate(c[0], c[1])).ToArray()) { SRID = 4326 }).ToArray();
        return new Polygon(lines[0], lines.Skip(1).ToArray()) { SRID = 4326 };
    }
    private static PlantHealthImportOutcome Reject(string code, string message) => new(PlantHealthImportDisposition.Rejected,
        ErrorCode: $"PlantHealthAnalysisReadyV4.{code}", Error: message);
    private static PlantHealthImportOutcome Retry(string message) => new(PlantHealthImportDisposition.Retry,
        ErrorCode: "PlantHealthAnalysisReadyV4.DependencyNotReady", Error: message);
    private static PlantHealthImportOutcome Complete(PlantHealthImportBatch batch, bool duplicate) => new(
        PlantHealthImportDisposition.Completed, batch.SurveyResultId, batch.Id, duplicate, batch.BlockingCount);
}
