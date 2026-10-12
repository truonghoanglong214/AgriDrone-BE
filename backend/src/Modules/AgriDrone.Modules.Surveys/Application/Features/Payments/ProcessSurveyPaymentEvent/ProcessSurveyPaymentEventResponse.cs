namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;

public sealed record ProcessSurveyPaymentEventResponse(
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
