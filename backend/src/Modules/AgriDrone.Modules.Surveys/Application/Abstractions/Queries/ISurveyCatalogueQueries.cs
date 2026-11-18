using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal interface ISurveyCatalogueQueries
{
    Task<IReadOnlyList<PublicSurveyServiceCatalogueItem>>
        GetPublicCatalogueAsync(
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default);

    Task<PublicCatalogueV3Result?> GetPublicCatalogueV3Async(
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SurveyServiceCatalogueItem>> GetCatalogueAsync(
        DateTimeOffset evaluatedAt,
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

internal sealed record PublicCatalogueV3Result(
    DateTimeOffset EvaluatedAt,
    bool IsBookable,
    PublicCatalogueV3PolicySummary PolicySummary,
    PublicCatalogueV3SharedDeposit SharedDeposit,
    IReadOnlyList<PublicCatalogueV3ServiceItem> Services);

internal sealed record PublicCatalogueV3PolicySummary(
    Guid BundleVersionId,
    int BundleVersionNumber,
    string PolicyFingerprint,
    string PricingCurrency,
    int ClaimWindowDays,
    string ClaimTimeZoneId,
    bool ExcludeNoticeDate,
    bool RefundableBeforeCapture,
    bool RetainDepositAfterCapture,
    int AllowedFreeNoShowReschedules);

internal sealed record PublicCatalogueV3SharedDeposit(
    Guid DepositPolicyId,
    int VersionNumber,
    decimal Amount,
    string Currency);

internal sealed record PublicCatalogueV3ServiceItem(
    Guid SurveyServiceId,
    string Code,
    Guid RevisionId,
    int RevisionVersionNumber,
    string Name,
    string Description,
    bool MapsPoles,
    bool ScreensDisease,
    Guid PriceVersionId,
    int PriceVersionNumber,
    decimal PricePerPole,
    string Currency,
    IReadOnlyList<string> PreliminaryLimitations);

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
    EffectiveSurveyServicePrice? CurrentPrice);

internal sealed record SurveyServicePriceHistoryItem(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    decimal? PricePerPole,
    decimal? LegacyPricePerHa,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    decimal? MappingPricePerPole = null,
    decimal? PlantHealthPricePerPole = null)
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
    DateTimeOffset? EffectiveTo,
    decimal? MappingPricePerPole = null,
    decimal? PlantHealthPricePerPole = null);
