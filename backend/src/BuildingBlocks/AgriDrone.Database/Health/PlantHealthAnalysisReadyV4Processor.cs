using System.Text.Json;
using System.Text.Json.Serialization;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthAnalysisReadyV4Processor(
    IIntegrationMessageReader reader, IExecutionContextRunner runner) : IIntegrationMessageProcessor
{
    private static readonly JsonSerializerOptions StrictPayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<IntegrationMessageProcessingResult> ProcessAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        if (body.IsEmpty || body.Length > IntegrationContractLimits.MaximumMessageBodyBytes)
            return IntegrationMessageProcessingResult.DeadLetter("PlantHealthAnalysisReadyV4.InvalidPayload", "Body is empty or too large.");
        try
        {
            using var json = JsonDocument.Parse(body);
            RejectDuplicateMembers(json.RootElement);
            if (!json.RootElement.TryGetProperty("payload", out var payload))
                return IntegrationMessageProcessingResult.DeadLetter("PlantHealthAnalysisReadyV4.InvalidPayload", "Payload is required.");
            _ = payload.Deserialize<PlantHealthAnalysisReadyV4>(StrictPayloadOptions);
        }
        catch (JsonException error)
        {
            return IntegrationMessageProcessingResult.DeadLetter("PlantHealthAnalysisReadyV4.InvalidPayload", error.Message);
        }
        var read = reader.Read(body, IntegrationEventDescriptors.PlantHealthAnalysisReadyV4, V4BusinessContractValidators.Validate);
        if (!read.IsSuccess) return IntegrationMessageProcessingResult.DeadLetter(read.Errors[0].Code, string.Join("; ", read.Errors.Select(e => e.Message)));
        var envelope = read.Envelope!;
        // Producer ActorId is provenance only. Execution identity remains a background integration.
        var snapshot = ExecutionContextSnapshot.ForRabbitMq(envelope.TenantId, null, envelope.CorrelationId, envelope.MessageId);
        var result = IntegrationMessageProcessingResult.Retry("Handler did not return an outcome.");
        await runner.RunAsync<IIntegrationMessageHandler<PlantHealthAnalysisReadyV4>>(snapshot,
            async (handler, token) => { result = await handler.HandleAsync(envelope, token); }, cancellationToken);
        return result;
    }

    private static void RejectDuplicateMembers(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property is not allowed.");
                RejectDuplicateMembers(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateMembers(item);
    }
}
