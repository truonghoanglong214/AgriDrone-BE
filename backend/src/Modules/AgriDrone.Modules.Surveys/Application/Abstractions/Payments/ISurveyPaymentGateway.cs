using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentGateway
{
    string Provider { get; }

    Task<Result<PaymentSessionResult>> CreateSessionAsync(
        CreatePaymentSession request,
        CancellationToken cancellationToken = default);
}

public sealed record CreatePaymentSession(
    Guid PaymentId,
    Guid SurveyOrderId,
    string OrderNumber,
    decimal Amount,
    string Currency,
    DateTimeOffset RequestedAt);

public sealed record PaymentSessionResult(
    string ProviderReference,
    Uri RedirectUrl,
    string ClientToken,
    DateTimeOffset ExpiresAt);
