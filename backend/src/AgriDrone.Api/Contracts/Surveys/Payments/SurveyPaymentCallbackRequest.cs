using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

/// <summary>
/// Provider-neutral payment callback payload. The signature covers the exact
/// request JSON as <c>{unixTimestamp}.{rawJson}</c> using HMAC-SHA256.
/// </summary>
public sealed record SurveyPaymentCallbackRequest(
    [param: Required]
    [param: StringLength(200, MinimumLength = 1)]
    string ProviderReference,
    [param: Required]
    [param: StringLength(200, MinimumLength = 1)]
    string EventId,
    [param: Required]
    [param: StringLength(100, MinimumLength = 1)]
    string EventType,
    [param: Required]
    [param: Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    decimal Amount,
    [param: Required]
    [param: StringLength(3, MinimumLength = 3)]
    string Currency,
    [param: Required]
    DateTimeOffset OccurredAt);
