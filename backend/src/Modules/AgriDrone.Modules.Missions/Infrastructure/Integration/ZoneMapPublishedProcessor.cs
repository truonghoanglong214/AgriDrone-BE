using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Mapping.Validation;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class ZoneMapPublishedProcessor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<ZoneMapPublishedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<ZoneMapPublishedV1>
        Descriptor => IntegrationEventDescriptors.ZoneMapPublishedV1;

    protected override IReadOnlyList<string> ValidatePayload(
        ZoneMapPublishedV1? payload) =>
        ZoneMapPublishedV1Validator.Validate(payload);
}
