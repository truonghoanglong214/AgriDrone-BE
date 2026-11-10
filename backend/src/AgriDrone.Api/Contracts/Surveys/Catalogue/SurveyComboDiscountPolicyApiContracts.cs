namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record CreateSurveyComboDiscountPolicyRequest(
    decimal DiscountPercentage,
    int ExpectedVersion);

public sealed record SurveyComboDiscountPolicyApiResponse(
    Guid PolicyId,
    int VersionNumber,
    decimal DiscountPercentage,
    DateTimeOffset EffectiveFrom);

