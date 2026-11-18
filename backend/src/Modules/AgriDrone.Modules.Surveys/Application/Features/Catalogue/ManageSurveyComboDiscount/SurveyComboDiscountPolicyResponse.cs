namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.ManageSurveyComboDiscount;

public sealed record SurveyComboDiscountPolicyResponse(
    Guid PolicyId,
    int VersionNumber,
    decimal DiscountPercentage,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy);

