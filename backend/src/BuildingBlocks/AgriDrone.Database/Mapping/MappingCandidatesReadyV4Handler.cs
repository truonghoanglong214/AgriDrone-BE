using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using AgriDrone.SharedKernel.Domain.Spatial;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Mapping;

internal sealed class MappingCandidatesReadyV4Handler(
    MappingPublicationDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<MappingCandidatesReadyV4>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<MappingCandidatesReadyV4> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be1MappingCandidatesReadyV4,
            envelope,
            (context, token) => ApplyAsync(context, envelope, token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        MappingPublicationDbContext context,
        IntegrationEventEnvelope<MappingCandidatesReadyV4> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;
        var producedAt = payload.ProducedAt;
        var payloadFingerprint = ComputePayloadFingerprint(payload);

        var orderContext = await context.SurveyOrders.AsNoTracking()
            .Where(o => o.Id == payload.SurveyOrderId)
            .Select(o => new { o.TenantId, o.FarmId })
            .SingleOrDefaultAsync(cancellationToken);

        if (orderContext is null)
        {
            return InboxHandlerResult.Retry("The survey order is not available yet.");
        }

        if (envelope.TenantId.HasValue && orderContext.TenantId != envelope.TenantId.Value ||
            orderContext.FarmId != payload.FarmId)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.ContextMismatch",
                "MappingCandidatesReadyV4 does not match the survey order context.");
        }

        // Idempotency check: verify payload identity matches existing batch
        var existingBatch = await context.MappingCandidateBatches
            .SingleOrDefaultAsync(b => b.HandoffId == payload.HandoffId, cancellationToken);
        if (existingBatch is not null)
        {
            if (existingBatch.SurveyOrderId != payload.SurveyOrderId ||
                existingBatch.MissionId != payload.MissionId ||
                existingBatch.CaptureAttemptId != payload.CaptureAttemptId ||
                existingBatch.FarmId != payload.FarmId ||
                existingBatch.JobId != payload.JobId ||
                !string.Equals(existingBatch.PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal))
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.HandoffConflict",
                    "Idempotency conflict: HandoffId was previously used with a different aggregate context.");
            }

            return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
            {
                payload.SurveyOrderId,
                payload.MissionId,
                payload.HandoffId,
                BatchId = existingBatch.Id,
                Status = existingBatch.Status.ToString(),
                Idempotent = true
            }));
        }

        // Lock farm row FOR UPDATE with tenant isolation to prevent concurrent map ingestion
        var farm = await context.Farms
            .FromSqlInterpolated($"SELECT * FROM farm.farms WHERE id = {payload.FarmId} AND tenant_id = {orderContext.TenantId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (farm is null)
        {
            return InboxHandlerResult.Retry("The target farm is not available yet.");
        }

        var order = await context.SurveyOrders
            .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {payload.SurveyOrderId} AND tenant_id = {orderContext.TenantId} AND farm_id = {payload.FarmId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return InboxHandlerResult.Retry("The survey order is not available yet.");
        }

        var processingJob = await context.AiProcessingJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == payload.JobId, cancellationToken);
        if (processingJob is null ||
            processingJob.MissionId != payload.MissionId ||
            processingJob.JobType != AiJobType.Mapping ||
            processingJob.Status != AiJobStatus.Completed ||
            processingJob.ModelVersionId != payload.ModelVersionId ||
            processingJob.CompletedAt != payload.ProducedAt)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.InvalidProcessingJob",
                "The processing job provenance does not match the completed mapping run.");
        }

        MappingProcessingSnapshot? processingSnapshot;
        try
        {
            processingSnapshot = processingJob.Parameters.Deserialize<MappingProcessingSnapshot>();
        }
        catch (JsonException)
        {
            processingSnapshot = null;
        }

        if (processingSnapshot is null ||
            processingSnapshot.SourceVersion != processingJob.AttemptNumber ||
            processingSnapshot.GridBearingDeg != payload.GridBearingDeg ||
            processingSnapshot.RowSpacingM != payload.RowSpacingM ||
            processingSnapshot.PlantSpacingM != payload.PlantSpacingM ||
            !string.Equals(processingSnapshot.AlgorithmVersion, payload.AlgorithmVersion, StringComparison.Ordinal) ||
            !IsSha256(processingSnapshot.InputFingerprint) ||
            !IsSha256(processingSnapshot.OutputFingerprint))
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.ProcessingSnapshotMismatch",
                "The event spatial or algorithm metadata does not match the immutable mapping processing snapshot.");
        }

        var modelVersion = await context.AiModelVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == payload.ModelVersionId, cancellationToken);
        if (modelVersion is null || !string.Equals(modelVersion.Version, payload.ModelVersion, StringComparison.Ordinal))
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.InvalidModelVersion",
                "The AI model version provenance does not match the persisted model version.");
        }

        // Load and validate Mission context
        var mission = await context.DroneMissions.SingleOrDefaultAsync(
            m => m.Id == payload.MissionId &&
                 m.TenantId == order.TenantId &&
                 m.FarmId == order.FarmId &&
                 m.SurveyOrderId == order.Id, cancellationToken);
        if (mission is null)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.MissionMismatch",
                "Mission not found or does not match tenant, farm and order context.");
        }

        // Load and validate CaptureAttempt context
        var attempt = await context.CaptureAttempts
            .FromSqlInterpolated($"SELECT * FROM survey.capture_attempts WHERE id = {payload.CaptureAttemptId} AND order_id = {order.Id} AND mission_id = {payload.MissionId} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.CaptureAttemptMismatch",
                "CaptureAttempt not found or does not match tenant, farm, order and mission context.");
        }

        if (mission.CaptureAttemptId != payload.CaptureAttemptId)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.MissionCaptureAttemptMismatch",
                "Mission is not associated with the provided CaptureAttemptId.");
        }

        // Load and validate FarmBoundary
        var boundary = await context.FarmBoundaries.SingleOrDefaultAsync(
            b => b.Id == payload.FarmBoundaryVersionId &&
                 b.TenantId == order.TenantId &&
                 b.FarmId == payload.FarmId,
            cancellationToken);
        if (boundary is null)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.BoundaryMismatch",
                "Farm boundary version does not exist or does not belong to the target farm.");
        }

        if (mission.FarmBoundaryVersionId != payload.FarmBoundaryVersionId)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.MissionBoundaryMismatch",
                "Mission is not associated with the provided FarmBoundaryVersionId.");
        }

        // Only one open draft map allowed per farm
        var hasOpenDraft = await context.FarmBaseMapVersions.AnyAsync(
            m => m.TenantId == order.TenantId && m.FarmId == payload.FarmId && m.Status == FarmBaseMapStatus.Draft,
            cancellationToken);
        if (hasOpenDraft)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.OpenDraftExists",
                $"An open draft farm base map already exists for farm '{payload.FarmId}'.");
        }

        // Spatial policy resolution: must exist at producedAt (Fail-Closed)
        var effectivePolicy = await context.SpatialPolicies
            .Where(p => p.StreamKey == "DEFAULT" &&
                        p.EffectiveFrom <= producedAt &&
                        (p.EffectiveTo == null || p.EffectiveTo > producedAt))
            .OrderByDescending(p => p.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (effectivePolicy is null)
        {
            return InboxHandlerResult.PermanentFailure(
                "SpatialPolicy.NotFound",
                "No effective spatial policy exists for the survey capture time.");
        }

        var isV3 = order.Scope == ContractScope.V3;
        if (isV3 && (mission.Purpose != MissionPurpose.Capture ||
                     order.Status != SurveyOrderStatus.Processing ||
                     attempt.Status != CaptureAttemptStatus.Processing ||
                     order.FarmBoundaryVersionId != payload.FarmBoundaryVersionId ||
                     mission.SourceMapVersionId != payload.ExpectedCurrentFarmBaseMapVersionId ||
                     (!order.RequiresBaselineMapping &&
                      order.FarmBaseMapVersionId != payload.ExpectedCurrentFarmBaseMapVersionId)))
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.InvalidCaptureContext",
                "The V3 order and capture attempt must be Processing and match the mission and pinned mapping lineage.");
        }

        // Pointer Resolution
        var pointer = await context.FarmBaselinePointers
            .SingleOrDefaultAsync(p => p.FarmId == payload.FarmId && p.TenantId == order.TenantId, cancellationToken);

        FarmBaseMapVersion draftBaseMap;
        if (payload.MappingKind == MappingKind.FirstBaseline)
        {
            if (!order.RequiresBaselineMapping)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.NotBaselineOrder",
                    $"Survey order {order.Id} does not require baseline mapping.");
            }

            if (!isV3 && order.Status != SurveyOrderStatus.BaselineInProgress)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.InvalidOrderStatus",
                    $"Survey order {order.Id} is in status '{order.Status}' and cannot transition to awaiting baseline review.");
            }

            if (pointer is not null)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.BaselinePointerExists",
                    $"A baseline pointer already exists for farm '{payload.FarmId}'. First baseline mapping is not permitted.");
            }

            if (payload.ExpectedCurrentFarmBaseMapVersionId.HasValue)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.UnexpectedExpectedCurrentMap",
                    "FirstBaseline mapping must not specify ExpectedCurrentFarmBaseMapVersionId.");
            }

            var existingMapsCount = await context.FarmBaseMapVersions
                .CountAsync(m => m.TenantId == order.TenantId && m.FarmId == payload.FarmId, cancellationToken);
            if (existingMapsCount > 0)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.ExistingMapsFoundForFirstBaseline",
                    "Farm already has base map versions. FirstBaseline requires zero existing map versions.");
            }

            var mapContext = new FarmBaseMapPublicationContext(
                sourceSurveyOrderId: order.Id,
                tenantId: order.TenantId,
                farmId: payload.FarmId,
                versionNumber: 1,
                sourceMissionGroupId: null,
                sourceMissionId: payload.MissionId,
                farmBoundaryVersionId: boundary.Id,
                handoffId: payload.HandoffId,
                gridBearingDeg: payload.GridBearingDeg,
                rowSpacingM: payload.RowSpacingM,
                plantSpacingM: payload.PlantSpacingM);

            draftBaseMap = FarmBaseMapVersion.CreateFirstBaseline(mapContext, producedAt);
        }
        else if (payload.MappingKind == MappingKind.FollowUpSnapshot)
        {
            if (order.RequiresBaselineMapping)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.RequiresBaselineMapping",
                    $"Survey order {order.Id} requires baseline mapping but follow-up snapshot mapping was provided.");
            }

            if (order.Status != SurveyOrderStatus.Processing)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.InvalidOrderStatus",
                    $"Follow-up survey order {order.Id} must be in status 'Processing'. Current status: '{order.Status}'.");
            }

            if (pointer is null || pointer.TenantId != order.TenantId || pointer.FarmId != payload.FarmId)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.BaselinePointerNotFound",
                    $"No baseline pointer exists for farm '{payload.FarmId}'. Follow-up mapping requires an approved baseline pointer.");
            }

            if (!order.BaselineMapVersionId.HasValue || order.BaselineMapVersionId.Value != pointer.CurrentBaselineMapVersionId)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.OrderBaselinePointerMismatch",
                    "SurveyOrder BaselineMapVersionId does not match current farm baseline pointer.");
            }

            if (!payload.ExpectedCurrentFarmBaseMapVersionId.HasValue)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.MissingExpectedCurrentMap",
                    "FollowUpSnapshot requires ExpectedCurrentFarmBaseMapVersionId.");
            }

            var baselineMap = await context.FarmBaseMapVersions.SingleOrDefaultAsync(
                m => m.Id == pointer.CurrentBaselineMapVersionId && m.FarmId == payload.FarmId,
                cancellationToken);

            if (baselineMap is null || !baselineMap.IsBaseline)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.InvalidBaselineMap",
                    "The baseline map referenced by the farm baseline pointer does not exist or is not a baseline map.");
            }

            var latestAcceptedPredecessor = await context.FarmBaseMapVersions
                .Where(m => m.TenantId == order.TenantId &&
                            m.FarmId == payload.FarmId &&
                            (m.Status == FarmBaseMapStatus.InternalApproved || m.Status == FarmBaseMapStatus.Published))
                .OrderByDescending(m => m.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (latestAcceptedPredecessor is null)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.NoAcceptedPredecessorMap",
                    "No accepted predecessor map found for follow-up mapping.");
            }

            if (payload.ExpectedCurrentFarmBaseMapVersionId.Value != latestAcceptedPredecessor.Id)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.StalePredecessorMap",
                    $"Expected current map '{payload.ExpectedCurrentFarmBaseMapVersionId.Value}' does not match latest accepted map '{latestAcceptedPredecessor.Id}'.");
            }

            if (mission.SourceMapVersionId != latestAcceptedPredecessor.Id)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.MissionSourceMapMismatch",
                    $"Mission SourceMapVersionId '{mission.SourceMapVersionId}' does not match latest accepted map '{latestAcceptedPredecessor.Id}'.");
            }

            var nextVersionNumber = latestAcceptedPredecessor.VersionNumber + 1;
            var mapContext = new FarmBaseMapPublicationContext(
                sourceSurveyOrderId: order.Id,
                tenantId: order.TenantId,
                farmId: payload.FarmId,
                versionNumber: nextVersionNumber,
                sourceMissionGroupId: null,
                sourceMissionId: payload.MissionId,
                farmBoundaryVersionId: boundary.Id,
                handoffId: payload.HandoffId,
                gridBearingDeg: payload.GridBearingDeg,
                rowSpacingM: payload.RowSpacingM,
                plantSpacingM: payload.PlantSpacingM);

            draftBaseMap = FarmBaseMapVersion.CreateFollowUp(mapContext, latestAcceptedPredecessor, baselineMap, producedAt);
        }
        else
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.InvalidMappingKind",
                $"Unknown or unsupported MappingKind '{payload.MappingKind}'.");
        }

        context.FarmBaseMapVersions.Add(draftBaseMap);

        var batchId = Guid.NewGuid();
        var derivedBoundaryPolicyVersion = $"{effectivePolicy.StreamKey}_V{effectivePolicy.VersionNumber}";
        var batch = MappingCandidateBatch.Create(
            id: batchId,
            mappingKind: payload.MappingKind,
            handoffId: payload.HandoffId,
            tenantId: order.TenantId,
            farmId: payload.FarmId,
            surveyOrderId: order.Id,
            missionId: payload.MissionId,
            captureAttemptId: payload.CaptureAttemptId,
            farmBoundaryVersionId: boundary.Id,
            farmBaseMapVersionId: draftBaseMap.Id,
            expectedCurrentFarmBaseMapVersionId: payload.ExpectedCurrentFarmBaseMapVersionId,
            gridBearingDeg: payload.GridBearingDeg,
            rowSpacingM: payload.RowSpacingM,
            plantSpacingM: payload.PlantSpacingM,
            jobId: payload.JobId,
            payloadFingerprint: payloadFingerprint,
            modelVersionId: payload.ModelVersionId,
            modelVersion: payload.ModelVersion,
            algorithmVersion: payload.AlgorithmVersion,
            boundaryPolicyVersion: derivedBoundaryPolicyVersion,
            producedAt: producedAt,
            spatialPolicyVersionId: effectivePolicy.Id,
            spatialPolicyFingerprint: effectivePolicy.PolicyFingerprint,
            boundaryToleranceM: effectivePolicy.BoundaryToleranceM,
            matchingDistanceThresholdM: effectivePolicy.MatchingDistanceThresholdM,
            rowBearingToleranceDeg: effectivePolicy.RowBearingToleranceDeg,
            gridSpacingToleranceM: effectivePolicy.GridSpacingToleranceM,
            createdAt: producedAt);

        context.MappingCandidateBatches.Add(batch);

        // Pre-extract coordinates for PostGIS geography distance calculation
        var boundaryToleranceM = effectivePolicy.BoundaryToleranceM;
        for (var i = 0; i < payload.Candidates.Count; i++)
        {
            var c = payload.Candidates[i];
            Point point;
            if (c.OriginalPosition is not null && c.OriginalPosition.Coordinates.Count >= 2)
            {
                point = new Point(c.OriginalPosition.Coordinates[0], c.OriginalPosition.Coordinates[1])
                {
                    SRID = 4326
                };
            }
            else
            {
                point = new Point(c.Longitude, c.Latitude) { SRID = 4326 };
            }

            var spatialEvidence = await context.Database.SqlQuery<SpatialEvidence>($"""
                SELECT
                    ST_Within(ST_SetSRID(ST_MakePoint({point.X}, {point.Y}), 4326), geometry) AS "StrictlyInside",
                    ST_Distance(
                        ST_SetSRID(ST_MakePoint({point.X}, {point.Y}), 4326)::geography,
                        ST_Boundary(geometry)::geography) AS "DistanceM"
                FROM farm.farm_boundaries
                WHERE id = {payload.FarmBoundaryVersionId}
                  AND farm_id = {payload.FarmId}
                  AND tenant_id = {order.TenantId}
                """).SingleOrDefaultAsync(cancellationToken);

            if (spatialEvidence is null ||
                double.IsNaN(spatialEvidence.DistanceM) ||
                double.IsInfinity(spatialEvidence.DistanceM) ||
                spatialEvidence.DistanceM < 0)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.InvalidBoundaryGeometry",
                    "The approved boundary cannot produce valid strict spatial evidence.");
            }

            var strictlyInside = spatialEvidence.StrictlyInside;
            var distanceM = spatialEvidence.DistanceM;

            // Classification rule:
            // distance_m <= BoundaryToleranceM -> NEEDS_REVIEW
            // distance_m > tolerance and strictlyInside -> IN_BOUNDARY
            // distance_m > tolerance and !strictlyInside -> OUT_OF_BOUNDARY
            string boundaryClassification;
            if (distanceM <= boundaryToleranceM)
            {
                boundaryClassification = BoundaryClassificationsV3.NeedsReview;
            }
            else if (strictlyInside)
            {
                boundaryClassification = BoundaryClassificationsV3.InBoundary;
            }
            else
            {
                boundaryClassification = BoundaryClassificationsV3.OutOfBoundary;
            }

            var candidateId = c.CandidateId!.Value;

            var candidate = MappingCandidate.Create(
                id: Guid.NewGuid(),
                batchId: batchId,
                zoneId: null,
                candidateId: candidateId,
                sourceObservationId: c.SourceObservationId, // nullable, never synthesizes random Guid
                evidenceMediaAssetIds: c.EvidenceMediaAssetIds ?? [],
                originalPosition: point,
                proposedRowIndex: c.ProposedRowIndex,
                proposedColumnIndex: c.ProposedColumnIndex,
                positionConfidence: c.PositionConfidence,
                boundaryClassification: boundaryClassification,
                measuredBoundaryDistanceM: (decimal)distanceM,
                boundaryThresholdM: (decimal)boundaryToleranceM,
                proposedMatchedPlantId: c.MatchedPlantId,
                matchConfidence: c.MatchConfidence,
                createdAt: producedAt,
                treeId: c.TreeId);

            context.MappingCandidates.Add(candidate);
        }

        try
        {
            if (!isV3 && payload.MappingKind == MappingKind.FirstBaseline)
            {
                order.MarkBaselineCandidatesReady(producedAt);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (SurveyDomainException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.DomainError",
                exception.Message);
        }

        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
        {
            payload.SurveyOrderId,
            payload.MissionId,
            payload.HandoffId,
            BatchId = batchId,
            DraftBaseMapId = draftBaseMap.Id,
            Status = order.Status.ToString()
        }));
    }

    private static string ComputePayloadFingerprint(MappingCandidatesReadyV4 payload)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            payload.MappingKind,
            payload.SurveyOrderId,
            payload.MissionId,
            payload.CaptureAttemptId,
            payload.FarmId,
            payload.FarmBoundaryVersionId,
            payload.HandoffId,
            payload.JobId,
            payload.ModelVersionId,
            payload.ExpectedCurrentFarmBaseMapVersionId,
            payload.ProducedAt,
            payload.GridBearingDeg,
            payload.RowSpacingM,
            payload.PlantSpacingM,
            payload.ModelVersion,
            payload.AlgorithmVersion,
            Candidates = payload.Candidates
                .OrderBy(c => c.CandidateId)
                .Select(c => new
                {
                    c.CandidateId,
                    c.SourceObservationId,
                    EvidenceMediaAssetIds = c.EvidenceMediaAssetIds?.OrderBy(id => id),
                    c.Longitude,
                    c.Latitude,
                    c.OriginalPosition,
                    c.ProposedRowIndex,
                    c.ProposedColumnIndex,
                    c.PositionConfidence,
                    c.TreeId,
                    c.MatchedPlantId,
                    c.MatchConfidence,
                    c.Status,
                    c.Source
                })
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private sealed record SpatialEvidence(bool StrictlyInside, double DistanceM);
}
