using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

public interface IIntegrationMessageHandler<TPayload>
{
    Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken);
}
