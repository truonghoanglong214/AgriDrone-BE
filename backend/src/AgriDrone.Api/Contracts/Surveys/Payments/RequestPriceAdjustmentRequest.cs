using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record RequestPriceAdjustmentRequest(
    [property: Required]
    [property: Range(1, int.MaxValue)]
    int? NewConfirmedPoleCount,
    [property: Required]
    [property: StringLength(2000, MinimumLength = 1)]
    string Reason,
    [property: Required]
    [property: StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    [property: Required]
    [property: Range(1, uint.MaxValue)]
    uint? ExpectedOrderVersion);
