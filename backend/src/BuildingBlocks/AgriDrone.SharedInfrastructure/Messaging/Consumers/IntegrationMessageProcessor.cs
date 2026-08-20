using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

public abstract class IntegrationMessageProcessor<TPayload>(
    IIntegrationMessageReader messageReader,
    IExecutionContextRunner executionContextRunner)
    : IIntegrationMessageProcessor
{
    protected abstract IntegrationEventDescriptor<TPayload> Descriptor
    {
        get;
    }

    protected abstract IReadOnlyList<string> ValidatePayload(
        TPayload? payload);

    public async Task<IntegrationMessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        var readResult = messageReader.Read(
            body,
            Descriptor,
            ValidatePayload);
        if (!readResult.IsSuccess)
        {
            var firstError = readResult.Errors[0];
            var allErrors = string.Join(
                "; ",
                readResult.Errors.Select(error => error.Message));
            return IntegrationMessageProcessingResult.DeadLetter(
                firstError.Code,
                allErrors);
        }

        var envelope = readResult.Envelope!;
        var snapshot = ExecutionContextSnapshot.ForRabbitMq(
            envelope.TenantId,
            envelope.ActorId,
            envelope.CorrelationId,
            envelope.MessageId);
        var result = IntegrationMessageProcessingResult.Retry(
            "The integration handler did not return a result.");

        await executionContextRunner.RunAsync<
            IIntegrationMessageHandler<TPayload>>(
            snapshot,
            async (handler, token) =>
            {
                result = await handler.HandleAsync(envelope, token);
            },
            cancellationToken);

        return result;
    }
}
