namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyDeposit;

public sealed record SurveyDepositPolicyResponse(
    Guid PolicyId,
    int VersionNumber,
    decimal AmountVnd,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy);
