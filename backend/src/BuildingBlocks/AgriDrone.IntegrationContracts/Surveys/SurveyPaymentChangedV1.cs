namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyPaymentChangedV1(
    Guid SurveyPaymentId,
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    string Provider,
    string ProviderReference,
    decimal Amount,
    string Currency,
    string PaymentStatus,
    string ChangeType,
    string Disposition,
    DateTimeOffset ChangedAt);
