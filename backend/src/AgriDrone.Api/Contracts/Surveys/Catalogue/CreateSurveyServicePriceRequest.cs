namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record CreateSurveyServicePriceRequest(
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    uint ExpectedServiceVersion);
