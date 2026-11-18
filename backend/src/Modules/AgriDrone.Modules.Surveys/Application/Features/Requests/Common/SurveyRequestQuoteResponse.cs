namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

public sealed record SurveyRequestQuoteResponse(
    Guid BusinessPolicyVersionId,
    string PolicyFingerprint,
    Guid SurveyServiceRevisionId,
    int SurveyServiceRevisionVersion,
    Guid PriceVersionId,
    int PriceVersionNumber,
    decimal PricePerPole,
    string Currency,
    Guid DepositPolicyId,
    int DepositVersionNumber,
    decimal DepositAmount,
    int? EstimatedPoleCount,
    decimal? IndicativeTotalAmount,
    DateTimeOffset EvaluatedAt);
