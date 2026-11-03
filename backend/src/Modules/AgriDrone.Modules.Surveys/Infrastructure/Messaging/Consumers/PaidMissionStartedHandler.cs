using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;

internal sealed class PaidMissionStartedHandler(
    SurveysDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<PaidMissionStartedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<PaidMissionStartedV1> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be1PaidMissionStartedV1,
            envelope,
            (context, token) => ApplyAsync(context, envelope, token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        SurveysDbContext context,
        IntegrationEventEnvelope<PaidMissionStartedV1> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;
        var order = await context.SurveyOrders.SingleOrDefaultAsync(
            item => item.Id == payload.SurveyOrderId, cancellationToken);

        if (order is null)
            return InboxHandlerResult.Retry("The survey order is not available yet.");

        if (order.TenantId != payload.TenantId ||
            order.FarmId != payload.FarmId ||
            envelope.TenantId != payload.TenantId)
        {
            return InboxHandlerResult.PermanentFailure(
                "PaidMissionStartedV1.ContextMismatch",
                "PaidMissionStartedV1 does not match the survey order context.");
        }

        if (order.Status == SurveyOrderStatus.InProgress)
        {
            return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
            {
                payload.SurveyOrderId,
                payload.MissionId,
                Status = order.Status.ToString(),
                Idempotent = true
            }));
        }

        if (order.Status != SurveyOrderStatus.ReadyForPaidService)
        {
            return InboxHandlerResult.PermanentFailure(
                "PaidMissionStartedV1.InvalidOrderStatus",
                $"Survey order {order.Id} is in status '{order.Status}' and cannot start paid service.");
        }

        try
        {
            order.StartPaidService(payload.StartedAt);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (SurveyDomainException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                "PaidMissionStartedV1.DomainError",
                exception.Message);
        }

        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
        {
            payload.SurveyOrderId,
            payload.MissionId,
            Status = order.Status.ToString()
        }));
    }
}
