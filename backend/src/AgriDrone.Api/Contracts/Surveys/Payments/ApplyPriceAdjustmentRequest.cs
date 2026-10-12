using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record ApplyPriceAdjustmentRequest(
    [property: Required]
    [property: Range(1, uint.MaxValue)]
    uint? ExpectedOrderVersion,
    [property: Required]
    [property: Range(1, uint.MaxValue)]
    uint? ExpectedAdjustmentVersion,
    [property: Range(1, uint.MaxValue)]
    uint? ExpectedPaymentVersion);
