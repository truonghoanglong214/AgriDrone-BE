using AgriDrone.IntegrationContracts.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    internal interface IIdentityIntegrationOutbox
    {
        void Add<TPayload>(
            IntegrationEventEnvelope<TPayload> envelope,
            string? partitionKey = null);
    }
}
