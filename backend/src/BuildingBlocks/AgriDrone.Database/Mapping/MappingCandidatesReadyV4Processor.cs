using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Mapping;

internal sealed class MappingCandidatesReadyV4Processor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<MappingCandidatesReadyV4>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<MappingCandidatesReadyV4> Descriptor =>
        IntegrationEventDescriptors.MappingCandidatesReadyV4;

    protected override IReadOnlyList<string> ValidatePayload(MappingCandidatesReadyV4? payload) =>
        V3BusinessContractValidators.Validate(payload);
}
