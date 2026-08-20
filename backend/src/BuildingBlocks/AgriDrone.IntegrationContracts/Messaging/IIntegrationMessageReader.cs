namespace AgriDrone.IntegrationContracts.Messaging;

public interface IIntegrationMessageReader
{
    IntegrationMessageReadResult<TPayload> Read<TPayload>(
        ReadOnlyMemory<byte> body,
        IntegrationEventDescriptor<TPayload> descriptor,
        Func<TPayload?, IReadOnlyList<string>> payloadValidator);
}
