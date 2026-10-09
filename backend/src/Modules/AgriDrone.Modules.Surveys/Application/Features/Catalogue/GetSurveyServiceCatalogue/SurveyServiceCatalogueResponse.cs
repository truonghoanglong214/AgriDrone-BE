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
    IReadOnlyList<SurveyServicePriceCatalogueResponse> Prices);

public sealed record SurveyServicePriceCatalogueResponse(
    Guid PriceVersionId,
    decimal? PricePerPole,
    decimal? LegacyPricePerHa,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    bool IsLegacyPerHectarePrice);
