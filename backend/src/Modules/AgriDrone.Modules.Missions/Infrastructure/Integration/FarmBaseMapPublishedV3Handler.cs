using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class FarmBaseMapPublishedV3Handler(
    MissionsDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<FarmBaseMapPublishedV3>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<FarmBaseMapPublishedV3> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext, IntegrationConsumerNames.Be2FarmBaseMapPublishedV3,
            envelope, (context, token) => ApplyAsync(context, envelope, token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        MissionsDbContext context,
        IntegrationEventEnvelope<FarmBaseMapPublishedV3> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;
        if ((envelope.CausationId.HasValue &&
             envelope.CausationId != payload.CausationId) ||
            envelope.ActorId != payload.ConfirmedBy)
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapPublishedV3.EnvelopeMismatch",
                "Publication causation or confirmer does not match the event envelope.");

        var baseline = await context.DroneMissions.SingleOrDefaultAsync(
            mission => mission.Id == payload.SourceMissionId, cancellationToken);
        if (baseline is null)
            return InboxHandlerResult.Retry("The source baseline mission is not available yet.");

        if (baseline.TenantId != envelope.TenantId ||
            baseline.FarmId != payload.FarmId ||
            baseline.SurveyOrderId != payload.SurveyOrderId ||
            baseline.Purpose != MissionPurpose.BaselineMapping)
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapPublishedV3.MissionContextMismatch",
                "FarmBaseMapPublishedV3 does not match the source baseline mission context.");

        var missions = await context.DroneMissions.Where(mission =>
            mission.SurveyOrderId == payload.SurveyOrderId &&
            mission.TenantId == envelope.TenantId && mission.FarmId == payload.FarmId)
            .ToListAsync(cancellationToken);
        try
        {
            baseline.ApplyPublishedFarmBaseMap(payload.SurveyOrderId,
                payload.PublicationId, payload.FarmBaseMapVersionId, payload.PublishedAt);
            foreach (var mission in missions)
            {
                if (mission.Id != baseline.Id && mission.RequiresBaselineCompletion)
                    mission.ApplyPublishedFarmBaseMap(payload.SurveyOrderId,
                        payload.PublicationId, payload.FarmBaseMapVersionId,
                        payload.PublishedAt);
            }
        }
        catch (InvalidOperationException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                "FarmBaseMapPublishedV3.MissionStateInvalid", exception.Message);
        }

        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
        {
            payload.PublicationId, payload.SurveyOrderId, payload.SourceMissionId,
            payload.FarmBoundaryVersionId, payload.FarmBaseMapVersionId,
            UpdatedMissionCount = missions.Count
        }));
    }
}
