using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Health.Validation;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class HealthReviewStateChangedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<HealthReviewStateChangedV1>(
        messageReader,
        executionContextRunner)
{
    protected override
        IntegrationEventDescriptor<HealthReviewStateChangedV1>
        Descriptor =>
            IntegrationEventDescriptors
                .HealthReviewStateChangedV1;

    protected override IReadOnlyList<string> ValidatePayload(
        HealthReviewStateChangedV1? payload) =>
        HealthReviewStateChangedV1Validator.Validate(payload);
}