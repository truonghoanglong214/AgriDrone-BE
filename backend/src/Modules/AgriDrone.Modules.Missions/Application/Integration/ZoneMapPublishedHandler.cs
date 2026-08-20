using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Application.Integration;

internal sealed class ZoneMapPublishedHandler(
    MissionsDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<ZoneMapPublishedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<ZoneMapPublishedV1> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be2ZoneMapPublishedV1,
            envelope,
            (context, token) => ApplyPublishedMapAsync(
                context,
                envelope,
                token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyPublishedMapAsync(
        MissionsDbContext context,
        IntegrationEventEnvelope<ZoneMapPublishedV1> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;
        var mission = await context.DroneMissions.SingleOrDefaultAsync(
            candidate => candidate.Id == payload.MissionId,
            cancellationToken);
        if (mission is null)
        {
            return InboxHandlerResult.Retry(
                "The source Mission is not available yet.");
        }

        if (mission.TenantId != envelope.TenantId ||
            mission.FarmId != payload.FarmId ||
            mission.ZoneId != payload.ZoneId)
        {
            return InboxHandlerResult.PermanentFailure(
                ZoneMapPublishedErrorCodes.MissionContextMismatch,
                "ZoneMapPublished does not belong to the Mission tenant/Farm/Zone.");
        }

        try
        {
            mission.ApplyPublishedZoneMap(
                payload.ApprovalId,
                payload.MapVersionId,
                payload.PublishedAt);
        }
        catch (InvalidOperationException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                ZoneMapPublishedErrorCodes.MissionStateInvalid,
                exception.Message);
        }

        return InboxHandlerResult.Completed(
            JsonSerializer.Serialize(new
            {
                payload.MissionId,
                payload.MapVersionId,
                payload.ApprovalId
            }));
    }
}
