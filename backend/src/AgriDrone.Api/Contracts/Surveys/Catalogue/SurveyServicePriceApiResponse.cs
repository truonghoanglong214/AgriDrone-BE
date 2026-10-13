namespace AgriDrone.Api.Contracts.Surveys.Catalogue;

public sealed record CreateSurveyServicePriceApiResponse(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid? ClosedPriceVersionId,
    uint ServiceVersion,
    DateTimeOffset CreatedAt);

public sealed record SurveyServicePriceHistoryApiResponse(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    decimal? PricePerPole,
    decimal? LegacyPricePerHa,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    bool IsLegacyPerHectarePrice);
