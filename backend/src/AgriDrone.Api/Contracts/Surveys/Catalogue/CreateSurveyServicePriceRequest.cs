namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record CreateSurveyServicePriceRequest(
    decimal AmountPerPole,
    string Currency,
    uint ExpectedServiceVersion);
