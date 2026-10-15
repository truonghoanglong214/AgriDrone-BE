namespace AgriDrone.Modules.Surveys.Application.Features.Catalogue.CreateSurveyServicePrice;

public sealed record CreateSurveyServicePriceResponse(
    Guid PriceVersionId,
    Guid SurveyServiceId,
    decimal AmountPerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    Guid? ClosedPriceVersionId,
    uint ServiceVersion,
    DateTimeOffset CreatedAt);
