using System.Text.Json;
using System.Text.Json.Serialization;
using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.SharedInfrastructure.Messaging;

internal sealed class SystemTextJsonIntegrationMessageSerializer
    : IIntegrationMessageSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        WriteIndented = false
    };

    public byte[] Serialize<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var body = JsonSerializer.SerializeToUtf8Bytes(
            envelope,
            SerializerOptions);

        if (body.Length > IntegrationContractLimits.MaximumMessageBodyBytes)
        {
            throw new InvalidOperationException(
                $"Serialized integration message exceeds the {IntegrationContractLimits.MaximumMessageBodyBytes}-byte limit.");
        }

        return body;
    }

    public IntegrationEventEnvelope<TPayload>? Deserialize<TPayload>(
        ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<IntegrationEventEnvelope<TPayload>>(
            body,
            SerializerOptions);
}
