namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal sealed record RabbitMqPublishMessage(
    string Exchange,
    string RoutingKey,
    ReadOnlyMemory<byte> Body,
    string ContentType,
    string MessageId,
    string? CorrelationId,
    string? EventType,
    IReadOnlyDictionary<string, object?>? Headers = null);
