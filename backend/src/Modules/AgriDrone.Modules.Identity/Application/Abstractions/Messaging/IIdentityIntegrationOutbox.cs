using AgriDrone.IntegrationContracts.Messaging;
namespace AgriDrone.Modules.Identity.Application.Abstractions.Messaging
{
    internal interface IIdentityIntegrationOutbox
    {
        void Add<TPayload>(
            IntegrationEventEnvelope<TPayload> envelope,
            string? partitionKey = null);
    }
}
