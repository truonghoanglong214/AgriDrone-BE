using System.Text.Json;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentCallbackVerifier
{
    PaymentCallbackVerificationResult Verify(
        string provider,
        string rawPayload,
        string? timestamp,
        string? signature,
        string? authorization,
        DateTimeOffset receivedAt);
}

public sealed record PaymentCallbackVerificationResult(
    bool IsValid,
    string? FailureCode,
    VerifiedSurveyPaymentEvent? Event)
{
    public static PaymentCallbackVerificationResult Invalid(string code) =>
        new(false, code, null);

    public static PaymentCallbackVerificationResult Valid(
        VerifiedSurveyPaymentEvent paymentEvent) =>
        new(true, null, paymentEvent);

    public static PaymentCallbackVerificationResult Ignored() =>
        new(true, null, null);
}

public sealed record VerifiedSurveyPaymentEvent(
    string Provider,
    string ProviderReference,
    string ProviderEventId,
    string EventType,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt,
    JsonDocument Payload);
