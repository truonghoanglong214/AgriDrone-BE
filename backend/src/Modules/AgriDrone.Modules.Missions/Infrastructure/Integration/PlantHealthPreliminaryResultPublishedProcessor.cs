using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class PlantHealthPreliminaryResultPublishedProcessor(IIntegrationMessageReader reader, IExecutionContextRunner runner)
    : IntegrationMessageProcessor<PlantHealthPreliminaryResultPublishedV1>(reader, runner)
{
    protected override IntegrationEventDescriptor<PlantHealthPreliminaryResultPublishedV1> Descriptor => IntegrationEventDescriptors.PlantHealthPreliminaryResultPublishedV1;
    protected override IReadOnlyList<string> ValidatePayload(PlantHealthPreliminaryResultPublishedV1? payload) => PlantHealthPublicationValidators.Validate(payload);
}
