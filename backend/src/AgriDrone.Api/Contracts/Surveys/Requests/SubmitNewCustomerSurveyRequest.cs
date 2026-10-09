namespace AgriDrone.Api.Contracts.Surveys.Requests;

public sealed record SubmitNewCustomerSurveyRequest(
    Guid SurveyServiceId,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string FarmName,
    string FarmAddress,
    decimal ApproximateAreaHa,
    double Longitude,
    double Latitude,
    int? EstimatedPoleCount,
    DateTimeOffset? PreferredStartAt,
    DateTimeOffset? PreferredEndAt,
    string? Notes);
