using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.Payments;

public sealed record InitiateSurveyPaymentRequest(
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion);

public sealed record InitiateSurveyPaymentApiResponse(
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
