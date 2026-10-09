using System;
using System.Text.Json.Serialization;

namespace AgriDrone.IntegrationContracts.Messaging
{
    public sealed record IntegrationEventEnvelope<TPayload>(
    Guid MessageId,
    Guid CorrelationId,
    Guid? TenantId,
    Guid? ActorId,
    DateTimeOffset OccurredAt,
    int SchemaVersion,
    string EventType,
    TPayload Payload,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? CausationId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceSystem = null);
}
