namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record UpdateSurveyServiceMetadataRequest(
    string Name,
    string Description,
    uint ExpectedVersion);
