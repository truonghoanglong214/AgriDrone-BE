namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyPriceAdjustmentChangedV1(
    Guid PriceAdjustmentId,
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    string Status,
    int OldPoleCount,
    int NewPoleCount,
    decimal OldPricePerPole,
    decimal NewPricePerPole,
    decimal OldFinalPrice,
    decimal NewFinalPrice,
    string Currency,
    string ChangeType,
    DateTimeOffset ChangedAt);
