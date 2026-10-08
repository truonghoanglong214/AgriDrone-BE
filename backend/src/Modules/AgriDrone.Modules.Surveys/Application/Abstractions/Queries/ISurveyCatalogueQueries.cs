using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal interface ISurveyCatalogueQueries
{
    Task<IReadOnlyList<PublicSurveyServiceCatalogueItem>>
        GetPublicCatalogueAsync(
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyServiceCatalogueItem>> GetCatalogueAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyServicePriceHistoryItem>> GetPriceHistoryAsync(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default);

    Task<EffectiveSurveyServicePrice?> GetEffectivePerPolePriceAsync(
        Guid surveyServiceId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingPerPolePriceAsync(
        Guid surveyServiceId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludingPriceId = null,
        CancellationToken cancellationToken = default);
}

internal sealed record PublicSurveyServiceCatalogueItem(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    SurveyServiceType ServiceType,
    SurveyServiceStatus Status,
    Guid PriceVersionId,
    decimal PricePerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

internal sealed record SurveyServiceCatalogueItem(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    SurveyServiceType ServiceType,
    SurveyServiceStatus Status,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SurveyServicePriceHistoryItem> Prices);

internal sealed record SurveyServicePriceHistoryItem(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    decimal? PricePerPole,
    decimal? LegacyPricePerHa,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt)
{
    public bool IsLegacyPerHectarePrice =>
        PricePerPole is null && LegacyPricePerHa.HasValue;
}

internal sealed record EffectiveSurveyServicePrice(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    decimal PricePerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);
