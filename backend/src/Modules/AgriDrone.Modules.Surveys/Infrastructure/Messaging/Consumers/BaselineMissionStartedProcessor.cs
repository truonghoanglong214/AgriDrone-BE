using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.IntegrationContracts.Missions.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;

internal sealed class BaselineMissionStartedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<BaselineMissionStartedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<BaselineMissionStartedV1> Descriptor =>
        IntegrationEventDescriptors.BaselineMissionStartedV1;

    protected override IReadOnlyList<string> ValidatePayload(BaselineMissionStartedV1? payload) =>
        BaselineMissionStartedV1Validator.Validate(payload);
}
