using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;

namespace AgriDrone.SharedInfrastructure.Messaging.Outbox;

public sealed class OutboxMessageFactory(
    IIntegrationMessageSerializer serializer,
    TimeProvider timeProvider)
{
    public const string JsonContentType = "application/json";

    public OutboxMessage Create<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string routingKey,
        string? partitionKey = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        if (routingKey.Length >
            MessagingPersistenceLimits.MaximumRoutingKeyLength)
        {
            throw new ArgumentException(
                $"Routing key cannot exceed {MessagingPersistenceLimits.MaximumRoutingKeyLength} characters.",
                nameof(routingKey));
        }

        var normalizedPartitionKey = NormalizePartitionKey(partitionKey);
        var createdAt = timeProvider.GetUtcNow();

        if (createdAt < envelope.OccurredAt)
        {
            throw new InvalidOperationException(
                "Outbox creation time cannot be before the event occurrence time.");
        }

        return OutboxMessage.Create(
            envelope.MessageId,
            envelope.TenantId,
            envelope.CorrelationId,
            envelope.ActorId,
            envelope.EventType,
            envelope.SchemaVersion,
            routingKey.Trim(),
            serializer.Serialize(envelope),
            JsonContentType,
            normalizedPartitionKey,
            envelope.OccurredAt,
            createdAt);
    }

    private static string? NormalizePartitionKey(string? partitionKey)
    {
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            return null;
        }

        var normalized = partitionKey.Trim();

        if (normalized.Length >
            MessagingPersistenceLimits.MaximumPartitionKeyLength)
        {
            throw new ArgumentException(
                $"Partition key cannot exceed {MessagingPersistenceLimits.MaximumPartitionKeyLength} characters.",
                nameof(partitionKey));
        }

        return normalized;
    }
}
