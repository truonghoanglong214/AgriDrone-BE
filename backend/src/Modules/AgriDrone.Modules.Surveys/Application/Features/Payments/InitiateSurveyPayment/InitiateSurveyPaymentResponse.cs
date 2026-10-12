namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;

public sealed record InitiateSurveyPaymentResponse(
    Guid SurveyPaymentId,
    Guid SurveyOrderId,
    Guid PaidServiceAppointmentId,
    decimal Amount,
    string Currency,
    string Provider,
    string ProviderReference,
    string PaymentStatus,
    Uri RedirectUrl,
    string ClientToken,
    DateTimeOffset ExpiresAt,
    uint PaymentVersion,
    uint OrderVersion,
    DateTimeOffset InitiatedAt);
