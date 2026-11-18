namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record CreateSurveyDepositPolicyRequest(
    decimal AmountVnd,
    int ExpectedVersion);

public sealed record SurveyDepositPolicyApiResponse(
    Guid PolicyId,
    int VersionNumber,
    decimal AmountVnd,
    DateTimeOffset EffectiveFrom);
