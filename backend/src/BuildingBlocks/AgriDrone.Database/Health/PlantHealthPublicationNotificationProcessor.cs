using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthPublicationNotificationProcessor(IIntegrationMessageReader reader, IExecutionContextRunner runner)
    : IntegrationMessageProcessor<PlantHealthPublicationNotificationRequestedV1>(reader, runner)
{
    protected override IntegrationEventDescriptor<PlantHealthPublicationNotificationRequestedV1> Descriptor => IntegrationEventDescriptors.PlantHealthPublicationNotificationRequestedV1;
    protected override IReadOnlyList<string> ValidatePayload(PlantHealthPublicationNotificationRequestedV1? payload) => PlantHealthPublicationValidators.Validate(payload);
}
