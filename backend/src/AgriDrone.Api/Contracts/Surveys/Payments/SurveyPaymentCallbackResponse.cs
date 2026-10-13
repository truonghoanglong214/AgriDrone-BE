namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record SurveyPaymentCallbackResponse(
    Guid? SurveyPaymentId,
    Guid? SurveyOrderId,
    string Provider,
    string ProviderReference,
    string ProviderEventId,
    string EventType,
    string? PaymentStatus,
    string? OrderStatus,
    bool Duplicate,
    bool TransitionApplied,
    string Disposition,
    DateTimeOffset ProcessedAt);
