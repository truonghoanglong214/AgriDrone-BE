namespace AgriDrone.IntegrationContracts.Messaging;

public static class IntegrationEventEnvelopeFactory
{
    public static IntegrationEventEnvelope<TPayload> Create<TPayload>(
        IntegrationEventDescriptor<TPayload> descriptor,
        Guid messageId,
        Guid correlationId,
        Guid tenantId,
        Guid? actorId,
        DateTimeOffset occurredAt,
        TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfEqual(messageId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(correlationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);

        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.EventType);

        if (descriptor.EventType.Length >
            IntegrationContractLimits.MaximumEventTypeLength)
        {
            throw new ArgumentException(
                $"EventType cannot exceed {IntegrationContractLimits.MaximumEventTypeLength} characters.",
                nameof(descriptor));
        }

        if (descriptor.SchemaVersion <= 0)
        {
            throw new ArgumentException(
                "SchemaVersion must be greater than zero.",
                nameof(descriptor));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException(
                "ActorId cannot be an empty GUID when provided.",
                nameof(actorId));
        }

        if (descriptor.RequiresActorId && !actorId.HasValue)
        {
            throw new ArgumentException(
                $"ActorId is required for event '{descriptor.EventType}'.",
                nameof(actorId));
        }

        if (occurredAt == default || occurredAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "OccurredAt must be a non-default UTC timestamp.",
                nameof(occurredAt));
        }

        return new IntegrationEventEnvelope<TPayload>(
            messageId,
            correlationId,
            tenantId,
            actorId,
            occurredAt,
            descriptor.SchemaVersion,
            descriptor.EventType,
            payload);
    }
}
