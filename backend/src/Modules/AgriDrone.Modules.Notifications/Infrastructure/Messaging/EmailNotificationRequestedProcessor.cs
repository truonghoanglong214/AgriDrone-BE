using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Notifications;
using AgriDrone.IntegrationContracts.Notifications.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Notifications.Infrastructure.Messaging;

internal sealed class EmailNotificationRequestedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<EmailNotificationRequestedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<EmailNotificationRequestedV1>
        Descriptor =>
        IntegrationEventDescriptors.EmailNotificationRequestedV1;

    protected override IReadOnlyList<string> ValidatePayload(
        EmailNotificationRequestedV1? payload) =>
        EmailNotificationRequestedV1Validator.Validate(payload);
}
