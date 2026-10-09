using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Messaging;

internal interface ISurveyIntegrationOutbox
{
    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
