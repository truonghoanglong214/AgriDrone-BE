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
    SystemSurveyServiceCurrentPriceApiResponse? CurrentPrice);

public sealed record SystemSurveyServiceCurrentPriceApiResponse(
    Guid PriceVersionId,
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

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
