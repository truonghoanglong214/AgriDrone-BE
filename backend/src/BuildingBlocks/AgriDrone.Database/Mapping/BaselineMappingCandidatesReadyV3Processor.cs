using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Mapping;

internal sealed class BaselineMappingCandidatesReadyV3Processor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<BaselineMappingCandidatesReadyV3>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<BaselineMappingCandidatesReadyV3> Descriptor =>
        IntegrationEventDescriptors.BaselineMappingCandidatesReadyV3;

    protected override IReadOnlyList<string> ValidatePayload(BaselineMappingCandidatesReadyV3? payload) =>
        V3BusinessContractValidators.Validate(payload);
}
