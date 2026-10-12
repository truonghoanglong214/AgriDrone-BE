namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyOrderPricingConfirmedV1(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    Guid SurveyServiceId,
    Guid FarmBaseMapVersionId,
    string PoleCountSource,
    int ConfirmedSurveyPoleCount,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency,
    decimal FinalPrice,
    Guid SystemManagerProfileId,
    DateTimeOffset ConfirmedAt);
