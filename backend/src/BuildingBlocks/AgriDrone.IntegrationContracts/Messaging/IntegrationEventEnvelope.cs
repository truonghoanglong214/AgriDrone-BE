using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Messaging
{
    public sealed record IntegrationEventEnvelope<TPayload>(
    Guid MessageId,
    Guid CorrelationId,
    Guid TenantId,
    Guid? ActorId,
    DateTimeOffset OccurredAt,
    int SchemaVersion,
    string EventType,
    TPayload Payload);
}
