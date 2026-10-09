namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record SystemSurveyServiceApiResponse(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    string ServiceType,
    string Status,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SystemSurveyServicePriceApiResponse> Prices);

public sealed record SystemSurveyServicePriceApiResponse(
    Guid PriceVersionId,
    decimal? PricePerPole,
    decimal? LegacyPricePerHa,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    bool IsLegacyPerHectarePrice);

public sealed record SurveyServiceMutationApiResponse(
    Guid SurveyServiceId,
    string Status,
    uint Version,
    DateTimeOffset UpdatedAt);

public sealed record SurveyServiceMetadataMutationApiResponse(
    Guid SurveyServiceId,
    string Code,
    string Name,
    string Description,
    string Status,
    uint Version,
    DateTimeOffset UpdatedAt);
