namespace AgriDrone.IntegrationContracts.Messaging;

public interface IIntegrationMessageSerializer
{
    byte[] Serialize<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope);

    IntegrationEventEnvelope<TPayload>? Deserialize<TPayload>(
        ReadOnlySpan<byte> body);
}
