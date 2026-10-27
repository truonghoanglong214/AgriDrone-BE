using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Surveys.Infrastructure.Payments;

internal sealed class SurveyPaymentCallbackVerifier(
    IOptions<SurveyPaymentProviderOptions> options)
    : ISurveyPaymentCallbackVerifier
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SurveyPaymentProviderOptions _options = options.Value;

    public PaymentCallbackVerificationResult Verify(
        string provider,
        string rawPayload,
        string? timestamp,
        string? signature,
        string? authorization,
        DateTimeOffset receivedAt)
    {
        if (!_options.Enabled ||
            !string.Equals(
                provider.Trim(),
                _options.Provider.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.ProviderNotSupported");
        }

        if (string.IsNullOrWhiteSpace(timestamp) ||
            !long.TryParse(
                timestamp,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var timestampSeconds))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.TimestampInvalid");
        }

        DateTimeOffset signedAt;
        try
        {
            signedAt = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.TimestampInvalid");
        }

        if ((receivedAt - signedAt).Duration() >
            TimeSpan.FromMinutes(_options.CallbackToleranceMinutes))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.TimestampExpired");
        }

        if (!HasValidSignature(timestamp, rawPayload, signature))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.SignatureInvalid");
        }

        CallbackPayload? callback;
        try
        {
            callback = JsonSerializer.Deserialize<CallbackPayload>(
                rawPayload,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        if (callback is null ||
            string.IsNullOrWhiteSpace(callback.ProviderReference) ||
            string.IsNullOrWhiteSpace(callback.EventId) ||
            string.IsNullOrWhiteSpace(callback.EventType) ||
            callback.Amount <= 0 ||
            string.IsNullOrWhiteSpace(callback.Currency) ||
            callback.OccurredAt == default ||
            callback.OccurredAt.Offset != TimeSpan.Zero ||
            callback.OccurredAt > receivedAt.AddMinutes(
                _options.CallbackToleranceMinutes))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        try
        {
            _ = PaymentEventIdentity.Create(
                provider,
                callback.ProviderReference,
                callback.EventId);
            var currency = CurrencyCode.Create(callback.Currency);
            var payload = JsonDocument.Parse(rawPayload);
            return PaymentCallbackVerificationResult.Valid(
                new VerifiedSurveyPaymentEvent(
                    provider.Trim().ToLowerInvariant(),
                    callback.ProviderReference.Trim(),
                    callback.EventId.Trim(),
                    callback.EventType.Trim().ToLowerInvariant(),
                    Money.Round(callback.Amount),
                    currency.Value,
                    callback.OccurredAt,
                    payload));
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            JsonException)
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }
    }

    private bool HasValidSignature(
        string timestamp,
        string rawPayload,
        string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var normalizedSignature = signature.Trim();
        if (normalizedSignature.StartsWith(
                "sha256=",
                StringComparison.OrdinalIgnoreCase))
        {
            normalizedSignature = normalizedSignature[7..];
        }

        byte[] receivedSignature;
        try
        {
            receivedSignature = Convert.FromHexString(normalizedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        var secret = Encoding.UTF8.GetBytes(_options.SigningSecret);
        var signedPayload = Encoding.UTF8.GetBytes(
            $"{timestamp}.{rawPayload}");
        var expectedSignature = HMACSHA256.HashData(secret, signedPayload);
        return receivedSignature.Length == expectedSignature.Length &&
               CryptographicOperations.FixedTimeEquals(
                   receivedSignature,
                   expectedSignature);
    }

    private sealed record CallbackPayload(
        string ProviderReference,
        string EventId,
        string EventType,
        decimal Amount,
        string Currency,
        DateTimeOffset OccurredAt);
}
