using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record ReconcileSurveyPaymentRequest(
    [param: Required]
    [param: StringLength(150, MinimumLength = 1)]
    string ProviderReference,
    [param: Required]
    [param: Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    decimal? SettledAmount,
    [param: Required]
    [param: StringLength(3, MinimumLength = 3)]
    string Currency,
    [param: Required]
    [param: StringLength(2000, MinimumLength = 1)]
    string Reason,
    [param: Required]
    [param: StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    [param: Required]
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedPaymentVersion,
    [param: Required]
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedOrderVersion);
