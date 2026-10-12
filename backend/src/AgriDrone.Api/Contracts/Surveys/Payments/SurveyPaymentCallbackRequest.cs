using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

/// <summary>
/// Provider-neutral payment callback payload. The signature covers the exact
/// request JSON as <c>{unixTimestamp}.{rawJson}</c> using HMAC-SHA256.
/// </summary>
public sealed record SurveyPaymentCallbackRequest(
    [property: Required]
    [property: StringLength(200, MinimumLength = 1)]
    string ProviderReference,
    [property: Required]
    [property: StringLength(200, MinimumLength = 1)]
    string EventId,
    [property: Required]
    [property: StringLength(100, MinimumLength = 1)]
    string EventType,
    [property: Required]
    [property: Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    decimal Amount,
    [property: Required]
    [property: StringLength(3, MinimumLength = 3)]
    string Currency,
    [property: Required]
    DateTimeOffset OccurredAt);
