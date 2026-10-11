using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyAppointmentSchedulingOutbox(
    SurveyAppointmentSchedulingDbContext context,
    OutboxMessageFactory outboxMessageFactory)
    : ISurveyAppointmentSchedulingOutbox
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
