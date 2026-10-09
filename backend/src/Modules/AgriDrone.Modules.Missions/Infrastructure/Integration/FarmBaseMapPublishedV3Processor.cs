using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class FarmBaseMapPublishedV3Processor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<FarmBaseMapPublishedV3>(
        messageReader, executionContextRunner)
{
    protected override IntegrationEventDescriptor<FarmBaseMapPublishedV3>
        Descriptor => IntegrationEventDescriptors.FarmBaseMapPublishedV3;

    protected override IReadOnlyList<string> ValidatePayload(
        FarmBaseMapPublishedV3? payload) =>
        V3BusinessContractValidators.Validate(payload);
}
