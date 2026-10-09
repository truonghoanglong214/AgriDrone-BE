namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetPublicSurveyServices;

public sealed record PublicSurveyServiceResponse(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    PublicSurveyServiceType ServiceType,
    bool IsExperimental,
    PublicIndicativePriceResponse IndicativePrice);

public sealed record PublicIndicativePriceResponse(
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public enum PublicSurveyServiceType
{
    PlantHealth,
    HarvestReadiness
}
