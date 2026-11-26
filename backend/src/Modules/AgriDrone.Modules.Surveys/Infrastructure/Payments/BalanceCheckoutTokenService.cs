using System.Security.Cryptography;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.SharedKernel.Application;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Surveys.Infrastructure.Payments;

internal sealed class BalanceCheckoutTokenService(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SurveyPaymentProviderOptions> paymentOptions,
    TimeProvider timeProvider)
    : IBalanceCheckoutTokenService
{
    private const string ProtectorPurpose = "AgriDrone.Surveys.BalanceCheckoutToken.v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector(ProtectorPurpose)
        .ToTimeLimitedDataProtector();

    private readonly SurveyPaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public string Protect(BalanceCheckoutTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var envelope = new TokenEnvelope(
            Version: 1,
            payload.ActorId,
            payload.TenantId,
            payload.FarmId,
            payload.OrderId,
            payload.ExpectedOrderVersion,
            payload.ExpectedInvoiceRevisionId,
            payload.ExpectedInvoiceFingerprint,
            payload.ExpectedApprovedResultRevisionId,
            payload.ExpectedApprovedResultFingerprint,
            payload.ExpectedCountSnapshotId,
            payload.ExpectedCountSnapshotFingerprint,
            payload.IssuedAt);

        var json = JsonSerializer.Serialize(envelope, SerializerOptions);
        var lifetime = TimeSpan.FromMinutes(_paymentOptions.BalanceCheckoutTokenLifetimeMinutes);
        return _protector.Protect(json, lifetime);
    }

    public Result<BalanceCheckoutTokenPayload> UnprotectAndValidate(
        string checkoutToken,
        Guid expectedActorId,
        Guid expectedTenantId,
        Guid expectedOrderId)
    {
        if (string.IsNullOrWhiteSpace(checkoutToken))
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Validation(
                "BalanceCheckout.CheckoutTokenInvalid",
                "Checkout token is required."));
        }

        string unprotectedJson;
        DateTimeOffset expiresAt;
        try
        {
            unprotectedJson = _protector.Unprotect(checkoutToken.Trim(), out expiresAt);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Validation(
                "BalanceCheckout.CheckoutTokenInvalid",
                "The checkout token is invalid or tampered. Reload the invoice and try again."));
        }

        if (expiresAt <= timeProvider.GetUtcNow())
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Validation(
                "BalanceCheckout.CheckoutTokenExpired",
                "The checkout token has expired. Reload the invoice and try again."));
        }

        TokenEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<TokenEnvelope>(unprotectedJson, SerializerOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Validation(
                "BalanceCheckout.CheckoutTokenInvalid",
                "The checkout token payload is invalid. Reload the invoice and try again."));
        }

        if (envelope is null ||
            envelope.Version != 1 ||
            envelope.ActorId == Guid.Empty ||
            envelope.TenantId == Guid.Empty ||
            envelope.FarmId == Guid.Empty ||
            envelope.OrderId == Guid.Empty ||
            envelope.ExpectedOrderVersion == 0 ||
            envelope.ExpectedInvoiceRevisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(envelope.ExpectedInvoiceFingerprint) ||
            envelope.ExpectedApprovedResultRevisionId == Guid.Empty ||
            string.IsNullOrWhiteSpace(envelope.ExpectedApprovedResultFingerprint) ||
            envelope.ExpectedCountSnapshotId == Guid.Empty ||
            string.IsNullOrWhiteSpace(envelope.ExpectedCountSnapshotFingerprint) ||
            envelope.IssuedAt == default)
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Validation(
                "BalanceCheckout.CheckoutTokenInvalid",
                "The checkout token payload is incomplete. Reload the invoice and try again."));
        }

        if (envelope.ActorId != expectedActorId || envelope.TenantId != expectedTenantId)
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Forbidden(
                "BalanceCheckout.CheckoutTokenActorMismatch",
                "The checkout token does not match the authenticated owner or tenant context."));
        }

        if (envelope.OrderId != expectedOrderId)
        {
            return Result.Failure<BalanceCheckoutTokenPayload>(AppError.Validation(
                "BalanceCheckout.CheckoutTokenOrderMismatch",
                "The checkout token does not match the target survey order."));
        }

        return Result.Success(new BalanceCheckoutTokenPayload(
            envelope.ActorId,
            envelope.TenantId,
            envelope.FarmId,
            envelope.OrderId,
            envelope.ExpectedOrderVersion,
            envelope.ExpectedInvoiceRevisionId,
            envelope.ExpectedInvoiceFingerprint,
            envelope.ExpectedApprovedResultRevisionId,
            envelope.ExpectedApprovedResultFingerprint,
            envelope.ExpectedCountSnapshotId,
            envelope.ExpectedCountSnapshotFingerprint,
            envelope.IssuedAt));
    }

    private sealed record TokenEnvelope(
        int Version,
        Guid ActorId,
        Guid TenantId,
        Guid FarmId,
        Guid OrderId,
        uint ExpectedOrderVersion,
        Guid ExpectedInvoiceRevisionId,
        string ExpectedInvoiceFingerprint,
        Guid ExpectedApprovedResultRevisionId,
        string ExpectedApprovedResultFingerprint,
        Guid ExpectedCountSnapshotId,
        string ExpectedCountSnapshotFingerprint,
        DateTimeOffset IssuedAt);
}
