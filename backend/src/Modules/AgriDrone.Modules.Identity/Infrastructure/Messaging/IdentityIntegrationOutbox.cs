using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Modules.Identity.Infrastructure.Messaging;

internal sealed class IdentityIntegrationOutbox(
    IdentityDbContext context,
    OutboxMessageFactory outboxMessageFactory)
    : IIdentityIntegrationOutbox
{
    public void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null)
    {
        var outboxMessage = outboxMessageFactory.Create(
            envelope,
            envelope.EventType,
            partitionKey);

        context.OutboxMessages.Add(outboxMessage);
    }
}
