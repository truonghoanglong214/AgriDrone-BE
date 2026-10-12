namespace AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;

public sealed record RequestPriceAdjustmentResponse(
    Guid PriceAdjustmentId,
    Guid SurveyOrderId,
    string Status,
    int OldPoleCount,
    int NewPoleCount,
    Guid OldSurveyServicePriceId,
    Guid NewSurveyServicePriceId,
    decimal OldPricePerPole,
    decimal NewPricePerPole,
    decimal OldFinalPrice,
    decimal NewFinalPrice,
    string Currency,
    string Reason,
    string EvidenceReference,
    Guid RequestedBy,
    uint AdjustmentVersion,
    uint OrderVersion,
    DateTimeOffset RequestedAt);
