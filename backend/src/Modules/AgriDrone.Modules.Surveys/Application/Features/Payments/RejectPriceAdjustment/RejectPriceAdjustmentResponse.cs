namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;

public sealed record RejectPriceAdjustmentResponse(
    Guid PriceAdjustmentId,
    Guid SurveyOrderId,
    string Status,
    Guid ReviewedBy,
    string ReviewReason,
    string ReviewEvidenceReference,
    uint AdjustmentVersion,
    uint OrderVersion,
    DateTimeOffset ReviewedAt);
