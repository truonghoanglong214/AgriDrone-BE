namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

public interface IIntegrationMessageProcessor
{
    Task<IntegrationMessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);
}
