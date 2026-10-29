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

internal sealed class BaselineMappingCandidatesReadyV3Handler(
    MappingPublicationDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<BaselineMappingCandidatesReadyV3>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<BaselineMappingCandidatesReadyV3> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be1BaselineMappingCandidatesReadyV3,
            envelope,
            (context, token) => ApplyAsync(context, envelope, token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        MappingPublicationDbContext context,
        IntegrationEventEnvelope<BaselineMappingCandidatesReadyV3> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;

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
                "BaselineMappingCandidatesReadyV3.ContextMismatch",
                "BaselineMappingCandidatesReadyV3 does not match the survey order context.");
        }

        var handoffId = payload.HandoffId.HasValue && payload.HandoffId.Value != Guid.Empty
            ? payload.HandoffId.Value
            : Guid.NewGuid();

        var existingBatch = await context.MappingCandidateBatches
            .SingleOrDefaultAsync(b => b.HandoffId == handoffId, cancellationToken);
        if (existingBatch is not null)
        {
            return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
            {
                payload.SurveyOrderId,
                payload.MissionId,
                HandoffId = handoffId,
                BatchId = existingBatch.Id,
                Status = existingBatch.Status.ToString(),
                Idempotent = true
            }));
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
                "BaselineMappingCandidatesReadyV3.InvalidOrderStatus",
                $"Survey order {order.Id} is in status '{order.Status}' and cannot transition to awaiting baseline review.");
        }

        if (!order.RequiresBaselineMapping)
        {
            return InboxHandlerResult.PermanentFailure(
                "BaselineMappingCandidatesReadyV3.NotBaselineOrder",
                $"Survey order {order.Id} does not require baseline mapping.");
        }

        var farm = await context.Farms.SingleOrDefaultAsync(
            f => f.Id == payload.FarmId, cancellationToken);
        if (farm is null)
        {
            return InboxHandlerResult.Retry("The target farm is not available yet.");
        }

        FarmBoundary? boundary = null;
        if (payload.FarmBoundaryVersionId.HasValue && payload.FarmBoundaryVersionId.Value != Guid.Empty)
        {
            boundary = await context.FarmBoundaries.SingleOrDefaultAsync(
                b => b.Id == payload.FarmBoundaryVersionId.Value, cancellationToken);
            if (boundary is null)
            {
                return InboxHandlerResult.Retry("The specified farm boundary version is not available yet.");
            }
            if (boundary.FarmId != payload.FarmId)
            {
                return InboxHandlerResult.PermanentFailure(
                    "BaselineMappingCandidatesReadyV3.BoundaryMismatch",
                    "Farm boundary version does not belong to the target farm.");
            }
        }
        else
        {
            boundary = await context.FarmBoundaries
                .Where(b => b.FarmId == payload.FarmId && b.Status == FarmBoundaryStatus.Approved)
                .OrderByDescending(b => b.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (boundary is null)
            {
                boundary = await context.FarmBoundaries
                    .Where(b => b.FarmId == payload.FarmId)
                    .OrderByDescending(b => b.VersionNumber)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (boundary is null)
            {
                return InboxHandlerResult.Retry("No approved farm boundary is available yet for this farm.");
            }
        }

        var farmBoundaryVersionId = boundary.Id;
        var producedAt = payload.ProducedAt ?? DateTimeOffset.UtcNow;

        var jobId = payload.JobId.HasValue && payload.JobId.Value != Guid.Empty
            ? payload.JobId.Value
            : Guid.NewGuid();
        var modelVersionId = payload.ModelVersionId.HasValue && payload.ModelVersionId.Value != Guid.Empty
            ? payload.ModelVersionId.Value
            : Guid.NewGuid();

        if (payload.ExpectedCurrentFarmBaseMapVersionId.HasValue)
        {
            var currentPublishedMap = await context.FarmBaseMapVersions
                .SingleOrDefaultAsync(
                    v => v.FarmId == payload.FarmId && v.Status == FarmBaseMapStatus.Published,
                    cancellationToken);

            if (currentPublishedMap is not null &&
                currentPublishedMap.Id != payload.ExpectedCurrentFarmBaseMapVersionId.Value)
            {
                return InboxHandlerResult.PermanentFailure(
                    "BaselineMappingCandidatesReadyV3.BaseMapVersionConflict",
                    $"Expected current base map version '{payload.ExpectedCurrentFarmBaseMapVersionId.Value}' does not match current published base map '{currentPublishedMap.Id}'.");
            }
        }

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
            farmBoundaryVersionId: farmBoundaryVersionId,
            handoffId: handoffId,
            gridBearingDeg: payload.GridBearingDeg,
            rowSpacingM: payload.RowSpacingM,
            plantSpacingM: payload.PlantSpacingM);

        var draftBaseMap = FarmBaseMapVersion.PrepareDraft(mapContext, producedAt);
        context.FarmBaseMapVersions.Add(draftBaseMap);

        var batchId = Guid.NewGuid();
        var batch = MappingCandidateBatch.Create(
            id: batchId,
            handoffId: handoffId,
            tenantId: order.TenantId,
            farmId: payload.FarmId,
            surveyOrderId: order.Id,
            missionId: payload.MissionId,
            farmBoundaryVersionId: farmBoundaryVersionId,
            farmBaseMapVersionId: draftBaseMap.Id,
            expectedCurrentFarmBaseMapVersionId: payload.ExpectedCurrentFarmBaseMapVersionId,
            gridBearingDeg: payload.GridBearingDeg,
            rowSpacingM: payload.RowSpacingM,
            plantSpacingM: payload.PlantSpacingM,
            jobId: jobId,
            modelVersionId: modelVersionId,
            modelVersion: payload.ModelVersion,
            algorithmVersion: payload.AlgorithmVersion,
            boundaryPolicyVersion: payload.BoundaryPolicyVersion,
            producedAt: producedAt,
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
                measuredBoundaryDistanceM: c.MeasuredBoundaryDistanceM ?? (isInside ? 5.0m : 0.0m),
                boundaryThresholdM: c.BoundaryThresholdM ?? 1.0m,
                proposedMatchedPlantId: c.MatchedPlantId,
                matchConfidence: c.MatchConfidence,
                createdAt: producedAt,
                treeId: c.TreeId);

            context.MappingCandidates.Add(candidate);
        }

        try
        {
            order.MarkBaselineCandidatesReady(producedAt);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (SurveyDomainException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                "BaselineMappingCandidatesReadyV3.DomainError",
                exception.Message);
        }

        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
        {
            payload.SurveyOrderId,
            payload.MissionId,
            HandoffId = handoffId,
            BatchId = batchId,
            DraftBaseMapId = draftBaseMap.Id,
            Status = order.Status.ToString()
        }));
    }
}
