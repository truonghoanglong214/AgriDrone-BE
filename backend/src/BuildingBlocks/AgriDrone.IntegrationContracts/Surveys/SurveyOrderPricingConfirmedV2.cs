namespace AgriDrone.IntegrationContracts.Surveys;

/// <summary>Two independently verified counts and their booked rates, settled as one Combo invoice.</summary>
public sealed record SurveyOrderPricingConfirmedV2(
    Guid SurveyOrderId, Guid TenantId, Guid FarmId, Guid SurveyServiceId, Guid FarmBaseMapVersionId,
    string PoleCountSource, Guid SurveyServicePriceId, int ConfirmedMappingPoleCount,
    int ConfirmedPlantHealthPoleCount, decimal MappingPricePerPole, decimal PlantHealthPricePerPole,
    decimal MappingSubtotal, decimal PlantHealthSubtotal, decimal ComboDiscountPercentage,
    decimal ComboDiscountAmount, string Currency, decimal FinalPrice, decimal RemainingAmount,
    Guid SystemManagerProfileId, DateTimeOffset ConfirmedAt);
