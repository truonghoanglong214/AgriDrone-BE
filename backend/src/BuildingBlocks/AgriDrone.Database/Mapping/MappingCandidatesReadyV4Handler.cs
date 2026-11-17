using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
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

        var order = await context.SurveyOrders.SingleOrDefaultAsync(
            o => o.Id == payload.SurveyOrderId, cancellationToken);

        if (order is null)
        {
            return InboxHandlerResult.Retry("The survey order is not available yet.");
        }

        if (envelope.TenantId.HasValue && order.TenantId != envelope.TenantId.Value ||
            order.FarmId != payload.FarmId)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.ContextMismatch",
                "MappingCandidatesReadyV4 does not match the survey order context.");
        }

        var existingBatch = await context.MappingCandidateBatches
            .SingleOrDefaultAsync(b => b.HandoffId == payload.HandoffId, cancellationToken);
        if (existingBatch is not null)
        {
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

        // Lock farm row FOR UPDATE to prevent concurrent map ingestion / race conditions
        var farm = await context.Farms
            .FromSqlInterpolated($"SELECT * FROM farm.farms WHERE id = {payload.FarmId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (farm is null)
        {
            return InboxHandlerResult.Retry("The target farm is not available yet.");
        }

        // Only one open draft map allowed per farm
        var hasOpenDraft = await context.FarmBaseMapVersions.AnyAsync(
            m => m.FarmId == payload.FarmId && m.Status == FarmBaseMapStatus.Draft,
            cancellationToken);
        if (hasOpenDraft)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.OpenDraftExists",
                $"An open draft farm base map already exists for farm '{payload.FarmId}'.");
        }

        // State machine check based on mapping kind
        if (payload.MappingKind == MappingKind.FirstBaseline)
        {
            if (!order.RequiresBaselineMapping)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.NotBaselineOrder",
                    $"Survey order {order.Id} does not require baseline mapping.");
            }

            if (order.Status is SurveyOrderStatus.AwaitingBaselineReview or SurveyOrderStatus.AwaitingPricing)
            {
                var existingBatchForOrder = await context.MappingCandidateBatches
                    .SingleOrDefaultAsync(b => b.SurveyOrderId == payload.SurveyOrderId, cancellationToken);
                if (existingBatchForOrder is not null)
                {
                    return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
                    {
                        payload.SurveyOrderId,
                        ExistingBatchId = existingBatchForOrder.Id,
                        Status = order.Status.ToString(),
                        Idempotent = true
                    }));
                }
            }

            if (order.Status != SurveyOrderStatus.BaselineInProgress)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.InvalidOrderStatus",
                    $"Survey order {order.Id} is in status '{order.Status}' and cannot transition to awaiting baseline review.");
            }
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
        }
        else
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.InvalidMappingKind",
                $"Unknown or unsupported MappingKind '{payload.MappingKind}'.");
        }

        // Farm boundary validation
        var boundary = await context.FarmBoundaries.SingleOrDefaultAsync(
            b => b.Id == payload.FarmBoundaryVersionId, cancellationToken);
        if (boundary is null)
        {
            return InboxHandlerResult.Retry("The specified farm boundary version is not available yet.");
        }
        if (boundary.FarmId != payload.FarmId)
        {
            return InboxHandlerResult.PermanentFailure(
                "MappingCandidatesReadyV4.BoundaryMismatch",
                "Farm boundary version does not belong to the target farm.");
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

        // Lineage & Pointer Resolution
        var pointer = await context.FarmBaselinePointers
            .SingleOrDefaultAsync(p => p.FarmId == payload.FarmId, cancellationToken);

        var maxVersionNumber = await context.FarmBaseMapVersions
            .Where(v => v.FarmId == payload.FarmId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var nextVersionNumber = maxVersionNumber + 1;

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

        FarmBaseMapVersion draftBaseMap;
        if (payload.MappingKind == MappingKind.FirstBaseline)
        {
            if (pointer is not null)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.BaselinePointerExists",
                    $"A baseline pointer already exists for farm '{payload.FarmId}'. First baseline mapping is not permitted.");
            }

            draftBaseMap = FarmBaseMapVersion.CreateFirstBaseline(mapContext, producedAt);
        }
        else
        {
            if (pointer is null)
            {
                return InboxHandlerResult.PermanentFailure(
                    "MappingCandidatesReadyV4.BaselinePointerNotFound",
                    $"No baseline pointer exists for farm '{payload.FarmId}'. Follow-up mapping requires an approved baseline pointer.");
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

            var latestApprovedMap = await context.FarmBaseMapVersions
                .Where(m => m.FarmId == payload.FarmId &&
                            (m.Status == FarmBaseMapStatus.InternalApproved || m.Status == FarmBaseMapStatus.Published))
                .OrderByDescending(m => m.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            var predecessor = latestApprovedMap ?? baselineMap;
            draftBaseMap = FarmBaseMapVersion.CreateFollowUp(mapContext, predecessor, baselineMap, producedAt);
        }

        context.FarmBaseMapVersions.Add(draftBaseMap);

        var batchId = Guid.NewGuid();
        var batch = MappingCandidateBatch.Create(
            id: batchId,
            mappingKind: payload.MappingKind,
            handoffId: payload.HandoffId,
            tenantId: order.TenantId,
            farmId: payload.FarmId,
            surveyOrderId: order.Id,
            missionId: payload.MissionId,
            farmBoundaryVersionId: boundary.Id,
            farmBaseMapVersionId: draftBaseMap.Id,
            expectedCurrentFarmBaseMapVersionId: payload.ExpectedCurrentFarmBaseMapVersionId,
            gridBearingDeg: payload.GridBearingDeg,
            rowSpacingM: payload.RowSpacingM,
            plantSpacingM: payload.PlantSpacingM,
            jobId: payload.JobId,
            modelVersionId: payload.ModelVersionId,
            modelVersion: payload.ModelVersion,
            algorithmVersion: payload.AlgorithmVersion,
            boundaryPolicyVersion: payload.BoundaryPolicyVersion,
            producedAt: producedAt,
            spatialPolicyVersionId: effectivePolicy.Id,
            spatialPolicyFingerprint: effectivePolicy.PolicyFingerprint,
            boundaryToleranceM: effectivePolicy.BoundaryToleranceM,
            matchingDistanceThresholdM: effectivePolicy.MatchingDistanceThresholdM,
            rowBearingToleranceDeg: effectivePolicy.RowBearingToleranceDeg,
            gridSpacingToleranceM: effectivePolicy.GridSpacingToleranceM,
            createdAt: producedAt);

        context.MappingCandidateBatches.Add(batch);

        foreach (var c in payload.Candidates)
        {
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

            var isInside = boundary.Geometry.Covers(point) || boundary.Geometry.Intersects(point);
            var boundaryClassification = !string.IsNullOrWhiteSpace(c.BoundaryClassification)
                ? c.BoundaryClassification
                : (isInside ? BoundaryClassificationsV3.InBoundary : BoundaryClassificationsV3.OutOfBoundary);

            var candidate = MappingCandidate.Create(
                id: Guid.NewGuid(),
                batchId: batchId,
                zoneId: null,
                candidateId: c.CandidateId is { } candidateId && candidateId != Guid.Empty
                    ? candidateId : Guid.NewGuid(),
                sourceObservationId: c.SourceObservationId is { } observationId && observationId != Guid.Empty
                    ? observationId : Guid.NewGuid(),
                evidenceMediaAssetIds: c.EvidenceMediaAssetIds ?? [],
                originalPosition: point,
                proposedRowIndex: c.ProposedRowIndex,
                proposedColumnIndex: c.ProposedColumnIndex,
                positionConfidence: c.PositionConfidence,
                boundaryClassification: boundaryClassification,
                measuredBoundaryDistanceM: c.MeasuredBoundaryDistanceM ?? (isInside ? (decimal)effectivePolicy.BoundaryToleranceM : 0.0m),
                boundaryThresholdM: c.BoundaryThresholdM ?? (decimal)effectivePolicy.BoundaryToleranceM,
                proposedMatchedPlantId: c.MatchedPlantId,
                matchConfidence: c.MatchConfidence,
                createdAt: producedAt,
                treeId: c.TreeId);

            context.MappingCandidates.Add(candidate);
        }

        try
        {
            if (payload.MappingKind == MappingKind.FirstBaseline)
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
}
