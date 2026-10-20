using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Missions;
using AgriDrone.IntegrationContracts.Missions.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Surveys.Infrastructure.Messaging.Consumers;

internal sealed class BaselineOrthophotoCompletedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<BaselineOrthophotoCompletedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<BaselineOrthophotoCompletedV1> Descriptor =>
        IntegrationEventDescriptors.BaselineOrthophotoCompletedV1;

    protected override IReadOnlyList<string> ValidatePayload(BaselineOrthophotoCompletedV1? payload) =>
        BaselineOrthophotoCompletedV1Validator.Validate(payload);
}
