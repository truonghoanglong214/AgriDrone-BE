using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
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

        var boundary = await context.FarmBoundaries.SingleOrDefaultAsync(
            b => b.Id == payload.FarmBoundaryVersionId, cancellationToken);
        if (boundary is null)
        {
            return InboxHandlerResult.Retry("The farm boundary version is not available yet.");
        }

        if (boundary.FarmId != payload.FarmId)
        {
            return InboxHandlerResult.PermanentFailure(
                "BaselineMappingCandidatesReadyV3.BoundaryMismatch",
                "Farm boundary version does not belong to the target farm.");
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
            farmBoundaryVersionId: payload.FarmBoundaryVersionId,
            handoffId: payload.HandoffId);

        var draftBaseMap = FarmBaseMapVersion.PrepareDraft(mapContext, payload.ProducedAt);
        context.FarmBaseMapVersions.Add(draftBaseMap);

        var batchId = Guid.NewGuid();
        var batch = MappingCandidateBatch.Create(
            id: batchId,
            handoffId: payload.HandoffId,
            tenantId: order.TenantId,
            farmId: payload.FarmId,
            surveyOrderId: order.Id,
            missionId: payload.MissionId,
            farmBoundaryVersionId: payload.FarmBoundaryVersionId,
            farmBaseMapVersionId: draftBaseMap.Id,
            expectedCurrentFarmBaseMapVersionId: payload.ExpectedCurrentFarmBaseMapVersionId,
            jobId: payload.JobId,
            modelVersionId: payload.ModelVersionId,
            modelVersion: payload.ModelVersion,
            algorithmVersion: payload.AlgorithmVersion,
            boundaryPolicyVersion: payload.BoundaryPolicyVersion,
            producedAt: payload.ProducedAt,
            createdAt: payload.ProducedAt);

        context.MappingCandidateBatches.Add(batch);

        foreach (var zonePayload in payload.Zones)
        {
            var candidateZoneId = Guid.NewGuid();
            var candidateZone = MappingCandidateZone.Create(
                id: candidateZoneId,
                batchId: batchId,
                zoneId: zonePayload.ZoneId,
                expectedCurrentZoneMapVersionId: zonePayload.ExpectedCurrentZoneMapVersionId,
                gridBearingDeg: zonePayload.GridBearingDeg,
                rowSpacingM: zonePayload.RowSpacingM,
                plantSpacingM: zonePayload.PlantSpacingM,
                createdAt: payload.ProducedAt);

            context.MappingCandidateZones.Add(candidateZone);

            foreach (var c in zonePayload.Candidates)
            {
                var point = new Point(c.OriginalPosition.Coordinates[0], c.OriginalPosition.Coordinates[1])
                {
                    SRID = 4326
                };

                var candidate = MappingCandidate.Create(
                    id: Guid.NewGuid(),
                    batchId: batchId,
                    zoneId: zonePayload.ZoneId,
                    candidateId: c.CandidateId,
                    sourceObservationId: c.SourceObservationId,
                    evidenceMediaAssetIds: c.EvidenceMediaAssetIds,
                    originalPosition: point,
                    proposedRowIndex: c.ProposedRowIndex,
                    proposedColumnIndex: c.ProposedColumnIndex,
                    positionConfidence: c.PositionConfidence,
                    boundaryClassification: c.BoundaryClassification,
                    measuredBoundaryDistanceM: c.MeasuredBoundaryDistanceM,
                    boundaryThresholdM: c.BoundaryThresholdM,
                    proposedMatchedPlantId: c.MatchedPlantId,
                    matchConfidence: c.MatchConfidence,
                    createdAt: payload.ProducedAt);

                context.MappingCandidates.Add(candidate);
            }
        }

        try
        {
            order.MarkBaselineCandidatesReady(payload.ProducedAt);
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
            payload.HandoffId,
            BatchId = batchId,
            DraftBaseMapId = draftBaseMap.Id,
            Status = order.Status.ToString()
        }));
    }
}
