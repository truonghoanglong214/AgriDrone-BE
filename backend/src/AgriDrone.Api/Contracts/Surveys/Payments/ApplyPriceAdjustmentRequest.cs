using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record ApplyPriceAdjustmentRequest(
    [param: Required]
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedOrderVersion,
    [param: Required]
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedAdjustmentVersion,
    [param: Range(1, uint.MaxValue)]
    uint? ExpectedPaymentVersion);
