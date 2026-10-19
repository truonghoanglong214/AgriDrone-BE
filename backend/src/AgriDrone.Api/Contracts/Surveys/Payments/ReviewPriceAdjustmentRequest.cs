using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record ReviewPriceAdjustmentRequest(
    [param: Required]
    [param: StringLength(2000, MinimumLength = 1)]
    string Reason,
    [param: Required]
    [param: StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    [param: Required]
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedOrderVersion,
    [param: Required]
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedAdjustmentVersion);
