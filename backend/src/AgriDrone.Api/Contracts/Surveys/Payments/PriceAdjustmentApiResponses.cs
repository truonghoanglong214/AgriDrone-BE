namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record RequestedPriceAdjustmentApiResponse(
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

public sealed record ReviewedPriceAdjustmentApiResponse(
    Guid PriceAdjustmentId,
    Guid SurveyOrderId,
    string Status,
    Guid ReviewedBy,
    string ReviewReason,
    string ReviewEvidenceReference,
    uint AdjustmentVersion,
    uint OrderVersion,
    DateTimeOffset ReviewedAt);

public sealed record AppliedPriceAdjustmentApiResponse(
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

public sealed record ReconcileSurveyPaymentApiResponse(
    Guid SurveyPaymentId,
    Guid SurveyOrderId,
    string Provider,
    string ProviderReference,
    decimal Amount,
    string Currency,
    string PaymentStatus,
    string OrderStatus,
    uint PaymentVersion,
    uint OrderVersion,
    Guid ReconciledBy,
    DateTimeOffset ReconciledAt);
