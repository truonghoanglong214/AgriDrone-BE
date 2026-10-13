using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;
using AgriDrone.Modules.Surveys.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Modules.Surveys.Infrastructure.Messaging;

internal sealed class SurveyIntegrationOutbox(
    SurveysDbContext context,
    OutboxMessageFactory outboxMessageFactory)
    : ISurveyIntegrationOutbox
{
    public void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        context.OutboxMessages.Add(outboxMessageFactory.Create(
            envelope,
            envelope.EventType,
            partitionKey));
    }
}
