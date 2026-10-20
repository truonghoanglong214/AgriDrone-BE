using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;

internal sealed class BaselineMissionStartedHandler(
    SurveysDbContext dbContext,
    InboxExecutionCoordinator inboxCoordinator)
    : IIntegrationMessageHandler<BaselineMissionStartedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<BaselineMissionStartedV1> envelope,
        CancellationToken cancellationToken) =>
        inboxCoordinator.ExecuteAsync(
            dbContext,
            IntegrationConsumerNames.Be1BaselineMissionStartedV1,
            envelope,
            (context, token) => ApplyAsync(context, envelope, token),
            cancellationToken);

    private static async Task<InboxHandlerResult> ApplyAsync(
        SurveysDbContext context,
        IntegrationEventEnvelope<BaselineMissionStartedV1> envelope,
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
                "BaselineMissionStartedV1.ContextMismatch",
                "BaselineMissionStartedV1 does not match the survey order context.");
        }

        if (order.Status == SurveyOrderStatus.BaselineInProgress)
        {
            return InboxHandlerResult.Completed(JsonSerializer.Serialize(new
            {
                payload.SurveyOrderId,
                payload.MissionId,
                Status = order.Status.ToString(),
                Idempotent = true
            }));
        }

        if (order.Status != SurveyOrderStatus.BaselineReady)
        {
            return InboxHandlerResult.PermanentFailure(
                "BaselineMissionStartedV1.InvalidOrderStatus",
                $"Survey order {order.Id} is in status '{order.Status}' and cannot start baseline mapping.");
        }

        try
        {
            order.StartBaselineMapping(payload.StartedAt);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (SurveyDomainException exception)
        {
            return InboxHandlerResult.PermanentFailure(
                "BaselineMissionStartedV1.DomainError",
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
