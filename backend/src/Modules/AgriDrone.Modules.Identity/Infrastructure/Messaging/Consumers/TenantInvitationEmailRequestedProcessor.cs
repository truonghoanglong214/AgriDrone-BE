using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Notifications.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Messaging.Consumers
{
    internal sealed class TenantInvitationEmailRequestedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner) : IntegrationMessageProcessor<TenantInvitationEmailRequestedV1>(
        messageReader,
        executionContextRunner)
    {
        protected override
            IntegrationEventDescriptor<TenantInvitationEmailRequestedV1>
            Descriptor =>
            IntegrationEventDescriptors.TenantInvitationEmailRequestedV1;

        protected override IReadOnlyList<string> ValidatePayload(
            TenantInvitationEmailRequestedV1? payload) =>
            TenantInvitationEmailRequestedV1Validator.Validate(payload);
    }
}
