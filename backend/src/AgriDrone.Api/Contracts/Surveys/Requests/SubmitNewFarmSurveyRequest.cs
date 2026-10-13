namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record SubmitNewFarmSurveyRequest(
    Guid SurveyServiceId,
    string FarmName,
    string FarmAddress,
    decimal ApproximateAreaHa,
    double Longitude,
    double Latitude,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes);
