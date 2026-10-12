namespace AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;

public sealed record ReconcileSurveyPaymentResponse(
    Guid SurveyPaymentId,
    Guid SurveyOrderId,
    string Provider,
    string ProviderReference,
    decimal Amount,
    string Currency,
    string PaymentStatus,
    string OrderStatus,
    uint PaymentVersion,
    uint OrderVersion,
    Guid ReconciledBy,
    DateTimeOffset ReconciledAt);
