using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class FarmBaseMapPublishedV2Processor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<FarmBaseMapPublishedV2>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<FarmBaseMapPublishedV2>
        Descriptor => IntegrationEventDescriptors.FarmBaseMapPublishedV2;

    protected override IReadOnlyList<string> ValidatePayload(
        FarmBaseMapPublishedV2? payload) =>
        V2BusinessContractValidators.Validate(payload);
}
