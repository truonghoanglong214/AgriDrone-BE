namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;

public sealed record ApplyPriceAdjustmentResponse(
    Guid PriceAdjustmentId,
    Guid SurveyOrderId,
    string Status,
    int ConfirmedPoleCount,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    decimal FinalPrice,
    string Currency,
    string OrderStatus,
    string? PaymentStatus,
    uint AdjustmentVersion,
    uint OrderVersion,
    uint? PaymentVersion,
    Guid AppliedBy,
    DateTimeOffset AppliedAt);
