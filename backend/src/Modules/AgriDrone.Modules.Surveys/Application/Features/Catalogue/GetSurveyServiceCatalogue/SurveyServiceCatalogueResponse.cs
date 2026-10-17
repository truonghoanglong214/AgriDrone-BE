using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServiceCatalogue;

public sealed record SurveyServiceCatalogueResponse(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    SurveyServiceType ServiceType,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SurveyServiceCurrentPriceResponse? CurrentPrice);

public sealed record SurveyServiceCurrentPriceResponse(
    Guid PriceVersionId,
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);
