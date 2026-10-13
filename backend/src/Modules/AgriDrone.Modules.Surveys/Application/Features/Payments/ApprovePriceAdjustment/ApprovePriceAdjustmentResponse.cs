namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ApprovePriceAdjustment;

public sealed record ApprovePriceAdjustmentResponse(
    Guid PriceAdjustmentId,
    Guid SurveyOrderId,
    string Status,
    Guid ReviewedBy,
    string ReviewReason,
    string ReviewEvidenceReference,
    uint AdjustmentVersion,
    uint OrderVersion,
    DateTimeOffset ReviewedAt);
