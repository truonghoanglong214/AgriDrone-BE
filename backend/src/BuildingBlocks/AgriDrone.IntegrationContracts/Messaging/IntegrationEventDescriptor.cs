namespace AgriDrone.IntegrationContracts.Messaging;

public sealed record IntegrationEventDescriptor<TPayload>(
    string EventType,
    int SchemaVersion,
    bool RequiresActorId);
