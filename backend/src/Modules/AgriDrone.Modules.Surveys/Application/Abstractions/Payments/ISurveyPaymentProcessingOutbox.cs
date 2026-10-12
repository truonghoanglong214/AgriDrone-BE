using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentProcessingOutbox
{
    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
