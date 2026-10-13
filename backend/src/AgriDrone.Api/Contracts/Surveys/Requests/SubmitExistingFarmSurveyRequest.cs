namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record SubmitExistingFarmSurveyRequest(
    Guid SurveyServiceId,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes);
