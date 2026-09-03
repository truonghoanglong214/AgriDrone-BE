using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class HealthReviewStateChangedHandler(
    MissionsDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<HealthReviewStateChangedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<HealthReviewStateChangedV1> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames
                .Be2HealthReviewStateChangedV1,
            envelope,
            (context, token) => ApplyAsync(
                context,
                envelope,
                token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        MissionsDbContext context,
        IntegrationEventEnvelope<HealthReviewStateChangedV1> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;

        var mission = await context.DroneMissions
            .SingleOrDefaultAsync(
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
                HealthReviewStateChangedErrorCodes
                    .MissionContextMismatch,
                "Health review state does not belong to the Mission tenant/Farm/Zone.");
        }

        try
        {
            mission.ApplyHealthReviewState(
                payload.HandoffId,
                payload.ReviewVersion,
                MapState(payload.State),
                payload.TotalObservations,
                payload.PendingReviews,
                payload.AwaitingFieldVerification,
                payload.ResolvedReviews,
                payload.ChangedAt);
        }
        catch (InvalidOperationException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                HealthReviewStateChangedErrorCodes
                    .MissionStateInvalid,
                exception.Message);
        }

        return InboxHandlerResult.Completed(
            JsonSerializer.Serialize(new
            {
                payload.HandoffId,
                payload.MissionId,
                payload.ReviewVersion,
                payload.State
            }));
    }

    private static MissionHealthReviewState MapState(
        string state) =>
        state switch
        {
            HealthReviewStates.Pending =>
                MissionHealthReviewState.Pending,

            HealthReviewStates.AwaitingFieldVerification =>
                MissionHealthReviewState
                    .AwaitingFieldVerification,

            HealthReviewStates.Resolved =>
                MissionHealthReviewState.Resolved,

            _ => throw new InvalidOperationException(
                $"Unsupported health review state '{state}'.")
        };
}