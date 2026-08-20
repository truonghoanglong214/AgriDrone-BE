using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Mapping.Validation;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Mapping;

internal sealed class MappingCandidatesApprovedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<MappingCandidatesApprovedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<MappingCandidatesApprovedV1>
        Descriptor => IntegrationEventDescriptors.MappingCandidatesApprovedV1;

    protected override IReadOnlyList<string> ValidatePayload(
        MappingCandidatesApprovedV1? payload) =>
        MappingCandidatesApprovedV1Validator.Validate(payload);
}
