namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.GetSurveyServicePriceHistory;

public sealed record SurveyServicePriceHistoryResponse(
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
