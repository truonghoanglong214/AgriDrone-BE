using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyPaymentProcessingOutbox(
    SurveyPaymentProcessingDbContext context,
    OutboxMessageFactory outboxMessageFactory)
    : ISurveyPaymentProcessingOutbox
{
    public void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        context.AddOutboxMessage(outboxMessageFactory.Create(
            envelope,
            envelope.EventType,
            partitionKey));
    }
}
