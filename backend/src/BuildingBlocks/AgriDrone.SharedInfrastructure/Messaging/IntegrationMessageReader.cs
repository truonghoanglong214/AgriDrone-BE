using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;

namespace AgriDrone.SharedInfrastructure.Messaging;

internal sealed class IntegrationMessageReader(
    IIntegrationMessageSerializer serializer,
    TimeProvider timeProvider) : IIntegrationMessageReader
{
    public IntegrationMessageReadResult<TPayload> Read<TPayload>(
        ReadOnlyMemory<byte> body,
        IntegrationEventDescriptor<TPayload> descriptor,
        Func<TPayload?, IReadOnlyList<string>> payloadValidator)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(payloadValidator);

        if (body.IsEmpty)
        {
            return Failure<TPayload>(
                IntegrationMessageErrorCodes.BodyEmpty,
                "Message body is required.");
        }

        if (body.Length > IntegrationContractLimits.MaximumMessageBodyBytes)
        {
            return Failure<TPayload>(
                IntegrationMessageErrorCodes.BodyTooLarge,
                $"Message body exceeds the {IntegrationContractLimits.MaximumMessageBodyBytes}-byte limit.");
        }

        IntegrationEventEnvelope<TPayload>? envelope;

        try
        {
            envelope = serializer.Deserialize<TPayload>(body.Span);
        }
        catch (JsonException exception)
        {
            return Failure<TPayload>(
                IntegrationMessageErrorCodes.MalformedJson,
                exception.Message);
        }
        catch (NotSupportedException exception)
        {
            return Failure<TPayload>(
                IntegrationMessageErrorCodes.MalformedJson,
                exception.Message);
        }

        var envelopeError = IntegrationEnvelopeValidator.Validate(
            envelope,
            descriptor.EventType,
            descriptor.SchemaVersion,
            timeProvider.GetUtcNow());

        if (envelopeError is not null)
        {
            return Failure<TPayload>(
                IntegrationMessageErrorCodes.EnvelopeInvalid,
                envelopeError);
        }

        if (descriptor.RequiresActorId && !envelope!.ActorId.HasValue)
        {
            return Failure<TPayload>(
                IntegrationMessageErrorCodes.ActorRequired,
                $"ActorId is required for event '{descriptor.EventType}'.");
        }

        var payloadErrors = payloadValidator(envelope!.Payload);
        if (payloadErrors.Count > 0)
        {
            return IntegrationMessageReadResult.Failure<TPayload>(
                payloadErrors
                    .Select(error => new IntegrationMessageError(
                        IntegrationMessageErrorCodes.PayloadInvalid,
                        error))
                    .ToArray());
        }

        return IntegrationMessageReadResult.Success(envelope);
    }

    private static IntegrationMessageReadResult<TPayload> Failure<TPayload>(
        string code,
        string message) =>
        IntegrationMessageReadResult.Failure<TPayload>(
            [new IntegrationMessageError(code, message)]);
}
