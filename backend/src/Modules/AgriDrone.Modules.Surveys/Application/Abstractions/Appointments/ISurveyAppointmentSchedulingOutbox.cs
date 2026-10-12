using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Appointments;

public interface ISurveyAppointmentSchedulingOutbox
{
    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
