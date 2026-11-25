using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyReleaseEvaluationRequestedV1Processor(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IntegrationMessageProcessor<SurveyReleaseEvaluationRequestedV1>(
        messageReader,
        executionContextRunner)
{
    protected override IntegrationEventDescriptor<SurveyReleaseEvaluationRequestedV1> Descriptor =>
        IntegrationEventDescriptors.SurveyReleaseEvaluationRequestedV1;

    protected override IReadOnlyList<string> ValidatePayload(SurveyReleaseEvaluationRequestedV1? payload) =>
        V3BusinessContractValidators.Validate(payload);
}
