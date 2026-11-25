using System.Text.Json;
using System.Text.Json.Serialization;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Health;

internal sealed class ComboAnalysisReadyV1Processor(
    IIntegrationMessageReader reader,
    IExecutionContextRunner runner) : IIntegrationMessageProcessor
{
    private static readonly JsonSerializerOptions StrictPayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<IntegrationMessageProcessingResult> ProcessAsync(
        ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (body.IsEmpty || body.Length > IntegrationContractLimits.MaximumMessageBodyBytes)
            return IntegrationMessageProcessingResult.DeadLetter("ComboAnalysisReadyV1.InvalidPayload", "Body is empty or too large.");

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return IntegrationMessageProcessingResult.DeadLetter("ComboAnalysisReadyV1.InvalidPayload", "Message body must be a JSON object.");
            RejectDuplicateMembers(json.RootElement);
            if (!json.RootElement.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                return IntegrationMessageProcessingResult.DeadLetter("ComboAnalysisReadyV1.InvalidPayload", "Payload must be a non-null JSON object.");
            _ = payload.Deserialize<ComboAnalysisReadyV1>(StrictPayloadOptions);
        }
        catch (JsonException error)
        {
            return IntegrationMessageProcessingResult.DeadLetter("ComboAnalysisReadyV1.InvalidPayload", error.Message);
        }

        var read = reader.Read(body, IntegrationEventDescriptors.ComboAnalysisReadyV1, ComboAnalysisReadyV1Validator.Validate);
        if (!read.IsSuccess)
            return IntegrationMessageProcessingResult.DeadLetter(read.Errors[0].Code, string.Join("; ", read.Errors.Select(e => e.Message)));

        var envelope = read.Envelope!;
        var snapshot = ExecutionContextSnapshot.ForRabbitMq(envelope.TenantId, null, envelope.CorrelationId, envelope.MessageId);
        var result = IntegrationMessageProcessingResult.Retry("Handler did not return an outcome.");

        await runner.RunAsync<IIntegrationMessageHandler<ComboAnalysisReadyV1>>(snapshot,
            async (handler, token) =>
            {
                result = await handler.HandleAsync(envelope, token);
            }, cancellationToken);

        return result;
    }

    private static void RejectDuplicateMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new JsonException("Duplicate JSON property is not allowed.");
                RejectDuplicateMembers(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                RejectDuplicateMembers(item);
        }
    }
}
