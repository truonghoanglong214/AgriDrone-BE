using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateBalanceCheckout;

internal sealed class InitiateBalanceCheckoutCommandHandler(
    IV3BalanceCheckoutTransaction balanceCheckoutTransaction,
    IBalanceCheckoutTokenService checkoutTokenService,
    ISurveyPaymentGateway paymentGateway,
    IOptions<SurveyPaymentProviderOptions> paymentOptions,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<InitiateBalanceCheckoutCommand, Result<InitiateBalanceCheckoutResponse>>
{
    private readonly SurveyPaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public async Task<Result<InitiateBalanceCheckoutResponse>> Handle(
        InitiateBalanceCheckoutCommand request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            actorId == Guid.Empty ||
            executionContext.TenantId is not Guid tenantId ||
            tenantId == Guid.Empty ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Result.Failure<InitiateBalanceCheckoutResponse>(
                AppError.Unauthorized(
                    "BalanceCheckout.Unauthorized",
                    "An authenticated TenantOwner with active tenant context is required."));
        }

        if (!_paymentOptions.Enabled || !paymentGateway.SupportsV3BalanceCheckout)
        {
            return Result.Failure<InitiateBalanceCheckoutResponse>(
                PaymentError.ProviderUnavailable());
        }

        var tokenResult = checkoutTokenService.UnprotectAndValidate(
            request.CheckoutToken,
            actorId,
            tenantId,
            request.OrderId);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<InitiateBalanceCheckoutResponse>(tokenResult.Error);
        }

        var token = tokenResult.Value;
        var provider = paymentGateway.Provider.Trim().ToLowerInvariant();
        var operationId = request.IdempotencyKey;
        var payloadFingerprint = BuildPayloadFingerprint(token, provider);

        var beginResult = await balanceCheckoutTransaction.BeginCheckoutIntentAsync(
            new V3BalanceCheckoutIntentRequest(
                OrderId: token.OrderId,
                TenantId: token.TenantId,
                ActorId: token.ActorId,
                OperationId: operationId,
                PayloadFingerprint: payloadFingerprint,
                ExpectedOrderVersion: token.ExpectedOrderVersion,
                ExpectedInvoiceRevisionId: token.ExpectedInvoiceRevisionId,
                ExpectedInvoiceFingerprint: token.ExpectedInvoiceFingerprint,
                ExpectedApprovedResultRevisionId: token.ExpectedApprovedResultRevisionId,
                ExpectedApprovedResultFingerprint: token.ExpectedApprovedResultFingerprint,
                ExpectedCountSnapshotId: token.ExpectedCountSnapshotId,
                ExpectedCountSnapshotFingerprint: token.ExpectedCountSnapshotFingerprint,
                Provider: provider,
                CorrelationId: executionContext.CorrelationId,
                ExpectedFarmId: token.FarmId),
            cancellationToken);

        if (beginResult.IsFailure)
        {
            return Result.Failure<InitiateBalanceCheckoutResponse>(beginResult.Error);
        }

        var intent = beginResult.Value;
        if (intent.Outcome == V3BalanceCheckoutOutcome.Replay)
        {
            if (intent.ExistingOutcome is { } existingOutcome)
            {
                return MapOutcome(existingOutcome, isReplay: true);
            }

            return Result.Failure<InitiateBalanceCheckoutResponse>(AppError.Conflict(
                "Checkout.UncertainProviderOutcome",
                "A checkout intent with this token already exists without a completed provider outcome and requires reconciliation."));
        }

        if (intent.Outcome != V3BalanceCheckoutOutcome.Created)
        {
            return MapFailureOutcome(
                intent.Outcome,
                intent.ErrorCode,
                intent.ErrorMessage);
        }

        if (!intent.PaymentId.HasValue ||
            !intent.DueAmount.HasValue ||
            string.IsNullOrWhiteSpace(intent.Currency) ||
            string.IsNullOrWhiteSpace(intent.OrderNumber) ||
            string.IsNullOrWhiteSpace(intent.ExistingProviderReference))
        {
            return Result.Failure<InitiateBalanceCheckoutResponse>(
                PaymentError.ProviderUnavailable());
        }

        bool providerSucceeded = false;
        bool providerOutcomeUncertain = false;
        PaymentSessionResult? session = null;
        string? failureCode = null;
        string? failureMessage = null;

        try
        {
            var sessionResult = await paymentGateway.CreateSessionAsync(
                new CreatePaymentSession(
                    intent.PaymentId.Value,
                    intent.OrderId,
                    intent.OrderNumber,
                    intent.DueAmount.Value,
                    intent.Currency,
                    intent.IntentCreatedAt ?? timeProvider.GetUtcNow(),
                    intent.ExistingProviderReference),
                cancellationToken);

            if (sessionResult.IsSuccess)
            {
                providerSucceeded = true;
                session = sessionResult.Value;
            }
            else
            {
                failureCode = sessionResult.Error.Code;
                failureMessage = sessionResult.Error.Description;
                providerOutcomeUncertain = string.Equals(
                    sessionResult.Error.Code,
                    PaymentError.ProviderOutcomeUncertainCode,
                    StringComparison.Ordinal);
            }
        }
        catch (HttpRequestException exception)
        {
            providerOutcomeUncertain = true;
            failureCode = PaymentError.ProviderOutcomeUncertainCode;
            failureMessage = exception.Message;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            providerOutcomeUncertain = true;
            failureCode = PaymentError.ProviderOutcomeUncertainCode;
            failureMessage = exception.Message;
        }

        var completeResult = await balanceCheckoutTransaction.CompleteCheckoutOutcomeAsync(
            new V3BalanceCheckoutOutcomeRequest(
                OrderId: intent.OrderId,
                PaymentId: intent.PaymentId.Value,
                ActorId: actorId,
                TenantId: tenantId,
                OperationId: operationId,
                ExpectedInvoiceRevisionId: token.ExpectedInvoiceRevisionId,
                ExpectedInvoiceFingerprint: token.ExpectedInvoiceFingerprint,
                ProviderSucceeded: providerSucceeded,
                ProviderOutcomeUncertain: providerOutcomeUncertain,
                ProviderReference: session?.ProviderReference ?? intent.ExistingProviderReference,
                RedirectUrl: session?.RedirectUrl,
                ClientToken: session?.ClientToken,
                ExpiresAt: session?.ExpiresAt,
                FailureCode: failureCode,
                FailureMessage: failureMessage,
                CorrelationId: executionContext.CorrelationId),
            cancellationToken);

        if (completeResult.IsFailure)
        {
            return Result.Failure<InitiateBalanceCheckoutResponse>(completeResult.Error);
        }

        return MapOutcome(completeResult.Value, isReplay: false);
    }

    private static Result<InitiateBalanceCheckoutResponse> MapOutcome(
        V3BalanceCheckoutResult result,
        bool isReplay)
    {
        if (result.Outcome is V3BalanceCheckoutOutcome.Created or V3BalanceCheckoutOutcome.Replay)
        {
            if (!result.PaymentId.HasValue ||
                !result.InvoiceRevisionId.HasValue ||
                !result.Amount.HasValue ||
                string.IsNullOrWhiteSpace(result.Currency) ||
                string.IsNullOrWhiteSpace(result.Provider) ||
                string.IsNullOrWhiteSpace(result.PaymentStatus))
            {
                return Result.Failure<InitiateBalanceCheckoutResponse>(
                    PaymentError.ProviderUnavailable());
            }

            return Result.Success(new InitiateBalanceCheckoutResponse(
                OrderId: result.OrderId,
                PaymentId: result.PaymentId.Value,
                InvoiceRevisionId: result.InvoiceRevisionId.Value,
                InvoiceRevisionNumber: result.InvoiceRevisionNumber,
                Amount: result.Amount.Value,
                Currency: result.Currency,
                Provider: result.Provider,
                ProviderReference: result.ProviderReference,
                PaymentStatus: result.PaymentStatus,
                RedirectUrl: result.RedirectUrl,
                ClientToken: result.ClientToken,
                ExpiresAt: result.ExpiresAt,
                PaymentVersion: result.PaymentVersion,
                OrderVersion: result.OrderVersion,
                IsReplay: isReplay || result.Outcome == V3BalanceCheckoutOutcome.Replay));
        }

        return MapFailureOutcome(result.Outcome, result.ErrorCode, result.ErrorMessage);
    }

    private static Result<InitiateBalanceCheckoutResponse> MapFailureOutcome(
        V3BalanceCheckoutOutcome outcome,
        string? errorCode,
        string? errorMessage)
    {
        var code = string.IsNullOrWhiteSpace(errorCode) ? "BalanceCheckout.Conflict" : errorCode;
        var message = string.IsNullOrWhiteSpace(errorMessage) ? "Balance checkout cannot proceed." : errorMessage;

        var error = outcome switch
        {
            V3BalanceCheckoutOutcome.NotFound => AppError.NotFound(code, message),
            V3BalanceCheckoutOutcome.Forbidden => AppError.Forbidden(code, message),
            V3BalanceCheckoutOutcome.ProviderUnavailable => AppError.Failure(code, message),
            V3BalanceCheckoutOutcome.ProviderUncertain => AppError.Failure(code, message),
            _ => AppError.Conflict(code, message)
        };

        return Result.Failure<InitiateBalanceCheckoutResponse>(error);
    }

    private static string BuildPayloadFingerprint(
        BalanceCheckoutTokenPayload token,
        string provider)
    {
        var canonical = string.Join(
            '|',
            "v3-balance-checkout-v1",
            token.ActorId.ToString("D"),
            token.TenantId.ToString("D"),
            token.FarmId.ToString("D"),
            token.OrderId.ToString("D"),
            token.ExpectedOrderVersion.ToString(CultureInfo.InvariantCulture),
            token.ExpectedInvoiceRevisionId.ToString("D"),
            token.ExpectedInvoiceFingerprint.Trim().ToLowerInvariant(),
            token.ExpectedApprovedResultRevisionId.ToString("D"),
            token.ExpectedApprovedResultFingerprint.Trim().ToLowerInvariant(),
            token.ExpectedCountSnapshotId.ToString("D"),
            token.ExpectedCountSnapshotFingerprint.Trim().ToLowerInvariant(),
            provider);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
