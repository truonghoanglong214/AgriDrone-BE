using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record ReconcileSurveyPaymentRequest(
    [property: Required]
    [property: StringLength(150, MinimumLength = 1)]
    string ProviderReference,
    [property: Required]
    [property: Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    decimal? SettledAmount,
    [property: Required]
    [property: StringLength(3, MinimumLength = 3)]
    string Currency,
    [property: Required]
    [property: StringLength(2000, MinimumLength = 1)]
    string Reason,
    [property: Required]
    [property: StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    [property: Required]
    [property: Range(1, uint.MaxValue)]
    uint? ExpectedPaymentVersion,
    [property: Required]
    [property: Range(1, uint.MaxValue)]
    uint? ExpectedOrderVersion);
