using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;

public interface ISurveyPricingConfirmationOutbox
{
    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
