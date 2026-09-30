namespace AgriDrone.Modules.Surveys.Application.Abstractions.Queries;

internal sealed record SurveyServicePriceRecord(
    Guid Id,
    Guid SurveyServiceId,
    decimal PricePerHa,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
