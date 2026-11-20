using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class FarmBaseMapInternallyApprovedV1Processor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<FarmBaseMapInternallyApprovedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<FarmBaseMapInternallyApprovedV1> Descriptor =>
        IntegrationEventDescriptors.FarmBaseMapInternallyApprovedV1;

    protected override IReadOnlyList<string> ValidatePayload(FarmBaseMapInternallyApprovedV1? payload) =>
        V3BusinessContractValidators.Validate(payload);
}
