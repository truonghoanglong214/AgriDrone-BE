namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record ChangeSurveyServiceStatusRequest(
    uint ExpectedVersion,
    string Reason);
