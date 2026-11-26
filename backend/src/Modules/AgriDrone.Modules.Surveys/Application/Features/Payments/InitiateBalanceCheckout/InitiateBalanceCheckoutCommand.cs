using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateBalanceCheckout;

public sealed record InitiateBalanceCheckoutCommand(
    Guid OrderId,
    string CheckoutToken,
    Guid IdempotencyKey)
    : IRequest<Result<InitiateBalanceCheckoutResponse>>;

public sealed record InitiateBalanceCheckoutResponse(
    Guid OrderId,
    Guid PaymentId,
    Guid InvoiceRevisionId,
    int? InvoiceRevisionNumber,
    decimal Amount,
    string Currency,
    string Provider,
    string? ProviderReference,
    string PaymentStatus,
    Uri? RedirectUrl,
    string? ClientToken,
    DateTimeOffset? ExpiresAt,
    uint? PaymentVersion,
    uint? OrderVersion,
    bool IsReplay);
