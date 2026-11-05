using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.IntegrationContracts.Missions.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;

internal sealed class PaidMissionStartedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<PaidMissionStartedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<PaidMissionStartedV1> Descriptor =>
        IntegrationEventDescriptors.PaidMissionStartedV1;

    protected override IReadOnlyList<string> ValidatePayload(PaidMissionStartedV1? payload) =>
        PaidMissionStartedV1Validator.Validate(payload);
}
