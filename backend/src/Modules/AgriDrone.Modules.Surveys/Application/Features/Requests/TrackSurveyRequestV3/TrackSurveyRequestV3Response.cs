using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;

public sealed record TrackSurveyRequestV3Response(
    string RequestNumber,
    SurveyRequestStatus Status,
    string ServiceName,
    int? EstimatedPoleCount,
    decimal? IndicativeTotalAmount,
    decimal DepositAmount,
    string Currency,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset SubmittedAt);
