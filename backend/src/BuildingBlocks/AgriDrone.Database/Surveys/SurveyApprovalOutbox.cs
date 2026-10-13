using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyApprovalOutbox(
    SurveyApprovalDbContext context,
    OutboxMessageFactory outboxMessageFactory)
    : ISurveyApprovalOutbox
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
