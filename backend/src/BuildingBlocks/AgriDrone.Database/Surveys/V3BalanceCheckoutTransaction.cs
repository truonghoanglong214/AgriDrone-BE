using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class V3BalanceCheckoutTransaction(
    AgriDroneSchemaDbContext db,
    OutboxMessageFactory outboxFactory,
    ISurveyPaymentGateway gateway,
    TimeProvider timeProvider) : IV3BalanceCheckoutTransaction
{
    private static readonly Regex Hex64Regex = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    public async Task<Result<V3BalanceCheckoutIntentResult>> BeginCheckoutIntentAsync(
        V3BalanceCheckoutIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("BeginCheckoutIntentAsync must own its SQL transaction and commit before external provider HTTP calls.");

        if (request.OrderId == Guid.Empty || request.TenantId == Guid.Empty || request.ActorId == Guid.Empty ||
            request.OperationId == Guid.Empty || request.ExpectedInvoiceRevisionId == Guid.Empty ||
            request.ExpectedApprovedResultRevisionId == Guid.Empty || request.ExpectedCountSnapshotId == Guid.Empty)
        {
            return Result.Failure<V3BalanceCheckoutIntentResult>(
                AppError.Validation("CheckoutIntent.InvalidIdentifiers", "Order, Tenant, Actor, Operation, Invoice, Approval, and Snapshot identifiers are required."));
        }

        var normalizedProvider = request.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedInvoiceFp = request.ExpectedInvoiceFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedApprovalFp = request.ExpectedApprovedResultFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var expectedCountFp = request.ExpectedCountSnapshotFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var suppliedFingerprint = request.PayloadFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!string.Equals(normalizedProvider, gateway.Provider, StringComparison.Ordinal) ||
            !Hex64Regex.IsMatch(suppliedFingerprint) ||
            !Hex64Regex.IsMatch(expectedInvoiceFp) ||
            !Hex64Regex.IsMatch(expectedApprovalFp) ||
            !Hex64Regex.IsMatch(expectedCountFp))
        {
            return Result.Failure<V3BalanceCheckoutIntentResult>(
                AppError.Validation("CheckoutIntent.InvalidInputs", "The configured provider and valid 64-hex payload and revision fingerprints are required."));
        }

        var fingerprint = Fingerprint(new
        {
            request.OrderId,
            request.ExpectedOrderVersion,
            request.ExpectedInvoiceRevisionId,
            expectedInvoiceFp,
            request.ExpectedApprovedResultRevisionId,
            expectedApprovalFp,
            request.ExpectedCountSnapshotId,
            expectedCountFp,
            normalizedProvider,
            suppliedFingerprint
        });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var scope = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == request.OrderId)
                .Select(o => new { o.Id, o.TenantId, o.FarmId })
                .SingleOrDefaultAsync(cancellationToken);

            if (scope is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.NotFound,
                    OrderId: request.OrderId,
                    ErrorCode: "Order.NotFound",
                    ErrorMessage: "Survey order was not found."));
            }

            if (scope.TenantId != request.TenantId)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.Forbidden,
                    OrderId: request.OrderId,
                    ErrorCode: "Payment.Forbidden",
                    ErrorMessage: "Order does not belong to caller tenant."));
            }

            // ADR 0020 Rank 1: Verify active Owner membership and Farm BEFORE reading payment/receipt rows
            if (!await HasActiveOwnerFarmAccessAsync(scope.TenantId, scope.FarmId, request.ActorId, cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.Forbidden,
                    OrderId: request.OrderId,
                    ErrorCode: "Payment.Forbidden",
                    ErrorMessage: "Caller is not an active TenantOwner for this farm."));
            }

            // ADR 0020 Rank 2: Lock Order FOR UPDATE
            var order = await db.Set<SurveyOrder>()
                .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {request.OrderId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.NotFound,
                    OrderId: request.OrderId,
                    ErrorCode: "Order.NotFound",
                    ErrorMessage: "Survey order was not found."));
            }

            var intentReceipt = await FindOperationAsync(
                request.TenantId, request.ActorId, request.OperationId,
                SurveyResultReviewOperation.ActionBeginBalanceCheckout, cancellationToken);
            if (intentReceipt is not null)
            {
                if (intentReceipt.SurveyOrderId != order.Id || intentReceipt.RequestFingerprint != fingerprint)
                    return Result.Failure<V3BalanceCheckoutIntentResult>(OperationConflict());

                var intent = ReadOutcome<V3BalanceCheckoutIntentResult>(intentReceipt);
                var receipt = await FindOperationAsync(
                    request.TenantId, request.ActorId, request.OperationId,
                    SurveyResultReviewOperation.ActionCompleteBalanceCheckout, cancellationToken)
                    ?? await FindOperationAsync(
                        request.TenantId, request.ActorId, request.OperationId,
                        SurveyResultReviewOperation.ActionBalanceCheckoutUncertain, cancellationToken);

                var paymentStatus = await db.Set<SurveyPayment>().AsNoTracking()
                    .Where(p => p.Id == intent.PaymentId && p.SurveyOrderId == order.Id)
                    .Select(p => (SurveyPaymentStatus?)p.Status)
                    .SingleOrDefaultAsync(cancellationToken);

                if (!HasCurrentCheckoutContext(order, intentReceipt) || paymentStatus == SurveyPaymentStatus.Invalidated)
                {
                    return Result.Success(intent with
                    {
                        Outcome = V3BalanceCheckoutOutcome.InvalidatedByCorrection,
                        OrderVersion = order.Version,
                        ErrorCode = "Checkout.InvalidatedByCorrection"
                    });
                }

                var existingOutcome = receipt is null ? null : ReadOutcome<V3BalanceCheckoutResult>(receipt);
                if (existingOutcome is not null && paymentStatus != SurveyPaymentStatus.Processing)
                    existingOutcome = existingOutcome with { RedirectUrl = null, ClientToken = null };

                await tx.CommitAsync(cancellationToken);
                return Result.Success(intent with
                {
                    Outcome = V3BalanceCheckoutOutcome.Replay,
                    ExistingOutcome = existingOutcome
                });
            }

            if (order.Scope != ContractScope.V3 ||
                order.Status != SurveyOrderStatus.AwaitingSettlement ||
                order.Version != request.ExpectedOrderVersion)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.StaleContext,
                    OrderId: order.Id,
                    OrderVersion: order.Version,
                    ErrorCode: "Checkout.StaleOrderState",
                    ErrorMessage: $"Order is not in the expected AwaitingSettlement version (status: {order.Status}, version: {order.Version})."));
            }

            // ADR 0020 Rank 5: Lock DepositObligation + active/open FinalBalance checkouts + funding
            SurveyDepositObligation? depositObligation = null;
            if (order.DepositObligationId.HasValue)
            {
                depositObligation = await db.Set<SurveyDepositObligation>()
                    .FromSqlInterpolated($"SELECT * FROM survey.survey_deposit_obligations WHERE id = {order.DepositObligationId.Value} AND survey_order_id = {order.Id} FOR SHARE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (depositObligation is null || depositObligation.Status == SurveyDepositObligationStatus.Cancelled)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Success(new V3BalanceCheckoutIntentResult(
                        Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                        OrderId: order.Id,
                        OrderVersion: order.Version,
                        ErrorCode: "DepositObligation.Invalid",
                        ErrorMessage: "Order deposit obligation is missing or cancelled."));
                }
            }

            var depositPayments = depositObligation is null
                ? new Dictionary<Guid, SurveyPayment>()
                : await db.Set<SurveyPayment>()
                    .FromSqlInterpolated($"""
                        SELECT *, xmin FROM survey.survey_payments
                        WHERE survey_order_id = {order.Id}
                          AND deposit_obligation_id = {depositObligation.Id}
                        ORDER BY id
                        FOR SHARE
                        """)
                    .ToDictionaryAsync(p => p.Id, cancellationToken);

            var activeBalanceCheckout = await db.Set<SurveyPayment>()
                .FromSqlInterpolated($"""
                    SELECT *, xmin FROM survey.survey_payments
                    WHERE survey_order_id = {order.Id}
                      AND purpose = 'FinalBalance'
                      AND status IN (
                        'PENDING'::system.survey_payment_status,
                        'PROCESSING'::system.survey_payment_status,
                        'PAID'::system.survey_payment_status,
                        'ADJUSTMENT_REQUIRED'::system.survey_payment_status)
                    ORDER BY id
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (activeBalanceCheckout is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ActiveCheckoutExists,
                    OrderId: order.Id,
                    PaymentId: activeBalanceCheckout.Id,
                    OrderNumber: order.OrderNumber,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: activeBalanceCheckout.InvoiceRevisionId,
                    DueAmount: activeBalanceCheckout.Amount,
                    Currency: activeBalanceCheckout.Currency,
                    Provider: activeBalanceCheckout.Provider,
                    ExistingProviderReference: activeBalanceCheckout.ProviderReference,
                    IntentCreatedAt: activeBalanceCheckout.CreatedAt,
                    ErrorCode: "Payment.ActivePaymentExists",
                    ErrorMessage: "An active FinalBalance checkout already exists for this order."));
            }

            var fundingResult = await SurveyInvoiceFunding.ReadAsync(
                db, order, depositObligation, depositPayments, lockRows: true, cancellationToken);
            if (fundingResult.IsFailure)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                    OrderId: order.Id,
                    OrderVersion: order.Version,
                    ErrorCode: fundingResult.Error.Code,
                    ErrorMessage: fundingResult.Error.Description));
            }

            // ADR 0020 Rank 6: Lock and verify exact V3-05F current invoice/approval/count revision tokens
            if (!order.CurrentInvoiceRevisionId.HasValue ||
                !order.CurrentApprovedResultRevisionId.HasValue ||
                !order.VerifiedPoleCountSnapshotId.HasValue)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.StaleContext,
                    OrderId: order.Id,
                    OrderVersion: order.Version,
                    ErrorCode: "Invoice.NotFinalized",
                    ErrorMessage: "Order is missing current finalized invoice pointers."));
            }

            var invoice = await db.Set<SurveyInvoiceRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_invoice_revisions WHERE id = {order.CurrentInvoiceRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            var approval = await db.Set<SurveyInternalApprovedResultRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_internal_approved_result_revisions WHERE id = {order.CurrentApprovedResultRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_order_pole_count_snapshots WHERE id = {order.VerifiedPoleCountSnapshotId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);

            if (invoice is null || approval is null || snapshot is null ||
                invoice.Status != SurveyInvoiceRevision.StatusCurrent ||
                !approval.IsCurrent ||
                invoice.ApprovedResultRevisionId != approval.Id ||
                invoice.PoleCountSnapshotId != snapshot.Id ||
                approval.PoleCountSnapshotId != snapshot.Id ||
                invoice.Id != request.ExpectedInvoiceRevisionId ||
                !string.Equals(invoice.EvidenceFingerprint, expectedInvoiceFp, StringComparison.Ordinal) ||
                approval.Id != request.ExpectedApprovedResultRevisionId ||
                !string.Equals(approval.EvidenceFingerprint, expectedApprovalFp, StringComparison.Ordinal) ||
                snapshot.Id != request.ExpectedCountSnapshotId ||
                !string.Equals(snapshot.EvidenceFingerprint, expectedCountFp, StringComparison.Ordinal))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.StaleContext,
                    OrderId: order.Id,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: invoice?.Id,
                    InvoiceRevisionNumber: invoice?.RevisionNumber,
                    InvoiceFingerprint: invoice?.EvidenceFingerprint,
                    ErrorCode: "Checkout.StaleInvoiceTokens",
                    ErrorMessage: "Invoice, approval, or count snapshot tokens no longer match the current finalized revision."));
            }

            var dueAmount = Math.Max(0m, invoice.TotalAmount - fundingResult.Value.ValidAllocatedDepositNet);
            if (dueAmount <= 0m)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ZeroBalanceSettled,
                    OrderId: order.Id,
                    OrderNumber: order.OrderNumber,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: invoice.Id,
                    InvoiceRevisionNumber: invoice.RevisionNumber,
                    InvoiceFingerprint: invoice.EvidenceFingerprint,
                    DueAmount: 0m,
                    Currency: invoice.Currency,
                    ErrorCode: "Checkout.ZeroBalanceDue",
                    ErrorMessage: "Finalized invoice has zero remaining balance due."));
            }

            var now = timeProvider.GetUtcNow();
            var payment = SurveyPayment.Create(
                order.Id,
                Money.Create(dueAmount, CurrencyCode.Create(invoice.Currency)),
                normalizedProvider,
                now,
                PaymentPurpose.FinalBalance,
                depositObligationId: null);

            var bindResult = payment.BindInvoiceRevision(invoice.Id);
            if (bindResult.IsFailure)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3BalanceCheckoutIntentResult>(bindResult.Error);
            }

            var providerReference = gateway.GetBalanceCheckoutReference(payment.Id);
            if (providerReference is null)
                return Result.Failure<V3BalanceCheckoutIntentResult>(PaymentError.ProviderUnavailable());

            var referenceResult = payment.BindBalanceCheckoutReference(providerReference);
            if (referenceResult.IsFailure)
                return Result.Failure<V3BalanceCheckoutIntentResult>(referenceResult.Error);

            db.Set<SurveyPayment>().Add(payment);

            using var auditDoc = JsonSerializer.SerializeToDocument(new
            {
                request.OperationId,
                payment.Id,
                payment.SurveyOrderId,
                payment.InvoiceRevisionId,
                invoice.RevisionNumber,
                invoice.EvidenceFingerprint,
                payment.Amount,
                payment.Currency,
                payment.Provider,
                status = "PENDING"
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                request.ActorId,
                request.CorrelationId,
                nameof(SurveyPayment),
                payment.Id,
                "INITIATE_V3_BALANCE_CHECKOUT_INTENT",
                null,
                auditDoc,
                now));

            var intentResult = new V3BalanceCheckoutIntentResult(
                Outcome: V3BalanceCheckoutOutcome.Created,
                OrderId: order.Id,
                PaymentId: payment.Id,
                OrderNumber: order.OrderNumber,
                OrderVersion: order.Version,
                InvoiceRevisionId: invoice.Id,
                InvoiceRevisionNumber: invoice.RevisionNumber,
                InvoiceFingerprint: invoice.EvidenceFingerprint,
                DueAmount: payment.Amount,
                Currency: payment.Currency,
                Provider: payment.Provider,
                ExistingProviderReference: payment.ProviderReference,
                IntentCreatedAt: now);
            var intentOperation = AddOperation(
                order, request.ActorId, request.OperationId, SurveyResultReviewOperation.ActionBeginBalanceCheckout,
                fingerprint, intentResult, now, invoice.Id, approval.Id, snapshot.Id);
            if (intentOperation.IsFailure)
                return Result.Failure<V3BalanceCheckoutIntentResult>(intentOperation.Error);

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result.Success(intentResult);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3BalanceCheckoutIntentResult>(AppError.Conflict(
                "CheckoutIntent.ConcurrencyConflict",
                "Checkout intent creation conflicted with a concurrent order or invoice mutation."));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3BalanceCheckoutIntentResult>(AppError.Conflict(
                "CheckoutIntent.ConcurrencyConflict",
                "Checkout intent creation conflicted with a concurrent order or invoice mutation."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<Result<V3BalanceCheckoutResult>> CompleteCheckoutOutcomeAsync(
        V3BalanceCheckoutOutcomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("CompleteCheckoutOutcomeAsync must run in a fresh transaction after the external provider call completes.");

        if (request.OrderId == Guid.Empty || request.PaymentId == Guid.Empty ||
            request.ActorId == Guid.Empty || request.TenantId == Guid.Empty ||
            request.OperationId == Guid.Empty || request.ExpectedInvoiceRevisionId == Guid.Empty)
        {
            return Result.Failure<V3BalanceCheckoutResult>(
                AppError.Validation("CheckoutOutcome.InvalidIdentifiers", "Order, Payment, Actor, Tenant, Operation, and Invoice identifiers are required."));
        }

        var invoiceFingerprint = request.ExpectedInvoiceFingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        var providerReference = request.ProviderReference?.Trim();
        if (!Hex64Regex.IsMatch(invoiceFingerprint) ||
            (request.ProviderSucceeded && (request.ProviderOutcomeUncertain || string.IsNullOrEmpty(providerReference) ||
                request.RedirectUrl is not { IsAbsoluteUri: true } || !request.ExpiresAt.HasValue)))
        {
            return Result.Failure<V3BalanceCheckoutResult>(
                AppError.Validation("CheckoutOutcome.InvalidInputs", "A valid invoice fingerprint and consistent provider outcome are required."));
        }

        var action = request.ProviderOutcomeUncertain
            ? SurveyResultReviewOperation.ActionBalanceCheckoutUncertain
            : SurveyResultReviewOperation.ActionCompleteBalanceCheckout;
        var fingerprint = Fingerprint(new
        {
            request.ProviderSucceeded,
            request.ProviderOutcomeUncertain,
            providerReference,
            request.RedirectUrl,
            request.ClientToken,
            ExpiresAt = request.ExpiresAt?.ToUniversalTime(),
            request.FailureCode,
            request.FailureMessage
        });

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var scope = await db.Set<SurveyOrder>().AsNoTracking()
                .Where(o => o.Id == request.OrderId)
                .Select(o => new { o.Id, o.TenantId, o.FarmId })
                .SingleOrDefaultAsync(cancellationToken);

            if (scope is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutResult(
                    Outcome: V3BalanceCheckoutOutcome.NotFound,
                    OrderId: request.OrderId,
                    PaymentId: request.PaymentId,
                    ErrorCode: "Order.NotFound",
                    ErrorMessage: "Survey order was not found."));
            }

            if (scope.TenantId != request.TenantId ||
                !await HasActiveOwnerFarmAccessAsync(scope.TenantId, scope.FarmId, request.ActorId, cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutResult(
                    Outcome: V3BalanceCheckoutOutcome.Forbidden,
                    OrderId: request.OrderId,
                    PaymentId: request.PaymentId,
                    ErrorCode: "Payment.Forbidden",
                    ErrorMessage: "Caller is not authorized for this farm."));
            }

            // ADR 0020 Rank 2 -> Rank 5: Lock Order FOR UPDATE before SurveyPayment FOR UPDATE
            var order = await db.Set<SurveyOrder>()
                .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {request.OrderId} FOR UPDATE")
                .SingleAsync(cancellationToken);

            var intentReceipt = await FindOperationAsync(
                request.TenantId, request.ActorId, request.OperationId,
                SurveyResultReviewOperation.ActionBeginBalanceCheckout, cancellationToken);
            if (intentReceipt is null || intentReceipt.SurveyOrderId != order.Id ||
                intentReceipt.InvoiceRevisionId != request.ExpectedInvoiceRevisionId)
                return Result.Failure<V3BalanceCheckoutResult>(OperationConflict());

            var intent = ReadOutcome<V3BalanceCheckoutIntentResult>(intentReceipt);
            if (intent.PaymentId != request.PaymentId || intent.InvoiceFingerprint != invoiceFingerprint)
                return Result.Failure<V3BalanceCheckoutResult>(OperationConflict());

            var receipt = await FindOperationAsync(
                request.TenantId, request.ActorId, request.OperationId, action, cancellationToken);
            if (receipt is not null && receipt.RequestFingerprint != fingerprint)
                return Result.Failure<V3BalanceCheckoutResult>(OperationConflict());

            var payment = await db.Set<SurveyPayment>()
                .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_payments WHERE id = {request.PaymentId} AND survey_order_id = {request.OrderId} AND purpose = 'FinalBalance' FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (payment is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutResult(
                    Outcome: V3BalanceCheckoutOutcome.NotFound,
                    OrderId: order.Id,
                    PaymentId: request.PaymentId,
                    OrderVersion: order.Version,
                    ErrorCode: "Payment.NotFound",
                    ErrorMessage: "Checkout intent payment was not found."));
            }

            var now = timeProvider.GetUtcNow();
            var effectiveAt = now >= payment.UpdatedAt ? now : payment.UpdatedAt;

            if (providerReference is not null &&
                !string.Equals(providerReference, payment.ProviderReference, StringComparison.Ordinal))
            {
                return Result.Failure<V3BalanceCheckoutResult>(AppError.Conflict(
                    "SurveyPayment.ProviderReferenceMismatch",
                    "Provider outcome does not match the stored checkout reference."));
            }

            if (!HasCurrentCheckoutContext(order, intentReceipt) ||
                payment.InvoiceRevisionId != intentReceipt.InvoiceRevisionId ||
                payment.Status == SurveyPaymentStatus.Invalidated)
            {
                return Result.Success(BuildOutcome(
                    V3BalanceCheckoutOutcome.InvalidatedByCorrection,
                    "Checkout.InvalidatedByCorrection",
                    "The checkout no longer belongs to the current settlement context."));
            }

            if (receipt is not null)
            {
                var prior = ReadOutcome<V3BalanceCheckoutResult>(receipt);
                if (prior.Outcome == V3BalanceCheckoutOutcome.Created)
                    prior = prior with { Outcome = V3BalanceCheckoutOutcome.Replay };
                if (payment.Status != SurveyPaymentStatus.Processing)
                    prior = prior with { RedirectUrl = null, ClientToken = null };

                await tx.CommitAsync(cancellationToken);
                return Result.Success(prior);
            }

            if (payment.Status != SurveyPaymentStatus.Pending)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutResult(
                    Outcome: V3BalanceCheckoutOutcome.StaleContext,
                    OrderId: order.Id,
                    PaymentId: payment.Id,
                    PaymentVersion: payment.Version,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: payment.InvoiceRevisionId,
                    ProviderReference: payment.ProviderReference,
                    PaymentStatus: payment.Status.ToString().ToUpperInvariant(),
                    ErrorCode: "Checkout.UnexpectedPaymentState",
                    ErrorMessage: $"Checkout intent is no longer Pending (current: {payment.Status})."));
            }

            if (request.ProviderOutcomeUncertain)
            {
                using var uncertainDoc = JsonSerializer.SerializeToDocument(new
                {
                    request.OperationId,
                    payment.Id,
                    payment.InvoiceRevisionId,
                    request.ProviderReference,
                    request.FailureCode,
                    request.FailureMessage
                });
                db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    request.CorrelationId,
                    nameof(SurveyPayment),
                    payment.Id,
                    "V3_BALANCE_CHECKOUT_PROVIDER_UNCERTAIN",
                    null,
                    uncertainDoc,
                    effectiveAt));

                return await CommitOutcomeAsync(
                    V3BalanceCheckoutOutcome.ProviderUncertain,
                    request.FailureCode ?? PaymentError.ProviderOutcomeUncertainCode,
                    request.FailureMessage ?? PaymentError.ProviderOutcomeUncertain().Description);
            }

            if (!request.ProviderSucceeded)
            {
                payment.Fail(effectiveAt);

                using var failDoc = JsonSerializer.SerializeToDocument(new
                {
                    request.OperationId,
                    payment.Id,
                    payment.InvoiceRevisionId,
                    request.FailureCode,
                    request.FailureMessage,
                    status = "FAILED"
                });
                db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    request.CorrelationId,
                    nameof(SurveyPayment),
                    payment.Id,
                    "V3_BALANCE_CHECKOUT_PROVIDER_FAILED",
                    null,
                    failDoc,
                    effectiveAt));

                return await CommitOutcomeAsync(
                    V3BalanceCheckoutOutcome.ProviderUnavailable,
                    request.FailureCode ?? PaymentError.ProviderUnavailableCode,
                    request.FailureMessage ?? PaymentError.ProviderUnavailable().Description);
            }

            payment.StartProcessing(providerReference!, effectiveAt);

            using var processingDoc = JsonSerializer.SerializeToDocument(new
            {
                request.OperationId,
                payment.Id,
                payment.SurveyOrderId,
                payment.InvoiceRevisionId,
                payment.Amount,
                payment.Currency,
                payment.Provider,
                payment.ProviderReference,
                paymentStatus = "PROCESSING",
                request.ExpiresAt
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                request.ActorId,
                request.CorrelationId,
                nameof(SurveyPayment),
                payment.Id,
                "INITIATE_V3_BALANCE_CHECKOUT",
                null,
                processingDoc,
                effectiveAt));

            var envelope = IntegrationEventEnvelopeFactory.Create(
                IntegrationEventDescriptors.SurveyPaymentChangedV1,
                Guid.CreateVersion7(),
                request.CorrelationId,
                order.TenantId,
                request.ActorId,
                effectiveAt,
                new SurveyPaymentChangedV1(
                    payment.Id,
                    order.Id,
                    order.TenantId,
                    order.FarmId,
                    payment.Provider,
                    payment.ProviderReference!,
                    payment.Amount,
                    payment.Currency,
                    "PROCESSING",
                    "PAYMENT_INITIATED",
                    "APPLIED",
                    effectiveAt));
            db.Set<OutboxMessage>().Add(outboxFactory.Create(
                envelope,
                IntegrationEventTypes.SurveyPaymentChangedV1,
                order.Id.ToString("D")));

            return await CommitOutcomeAsync(V3BalanceCheckoutOutcome.Created);

            V3BalanceCheckoutResult BuildOutcome(
                V3BalanceCheckoutOutcome outcome, string? errorCode = null, string? errorMessage = null) =>
                new(
                    Outcome: outcome,
                    OrderId: order.Id,
                    PaymentId: payment.Id,
                    PaymentVersion: payment.Version,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: payment.InvoiceRevisionId,
                    InvoiceRevisionNumber: intent.InvoiceRevisionNumber,
                    InvoiceFingerprint: invoiceFingerprint,
                    Amount: payment.Amount,
                    Currency: payment.Currency,
                    Provider: payment.Provider,
                    ProviderReference: payment.ProviderReference,
                    PaymentStatus: payment.Status.ToString().ToUpperInvariant(),
                    RedirectUrl: outcome == V3BalanceCheckoutOutcome.Created ? request.RedirectUrl : null,
                    ClientToken: outcome == V3BalanceCheckoutOutcome.Created ? request.ClientToken : null,
                    ExpiresAt: outcome == V3BalanceCheckoutOutcome.Created ? request.ExpiresAt : null,
                    ErrorCode: errorCode,
                    ErrorMessage: errorMessage);

            async Task<Result<V3BalanceCheckoutResult>> CommitOutcomeAsync(
                V3BalanceCheckoutOutcome outcome, string? errorCode = null, string? errorMessage = null)
            {
                // Read the payment version returned by PostgreSQL before recording the response.
                await db.SaveChangesAsync(cancellationToken);
                var result = BuildOutcome(outcome, errorCode, errorMessage);
                var operation = AddOperation(
                    order, request.ActorId, request.OperationId, action, fingerprint, result, effectiveAt,
                    intentReceipt.InvoiceRevisionId, intentReceipt.ApprovedResultRevisionId,
                    intentReceipt.PoleCountSnapshotId);
                if (operation.IsFailure)
                    return Result.Failure<V3BalanceCheckoutResult>(operation.Error);

                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result.Success(result);
            }

        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3BalanceCheckoutResult>(AppError.Conflict(
                "CheckoutOutcome.ConcurrencyConflict",
                "Checkout outcome persistence conflicted with a concurrent update."));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3BalanceCheckoutResult>(AppError.Conflict(
                "CheckoutOutcome.ConcurrencyConflict",
                "Checkout outcome persistence conflicted with a concurrent update."));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private static bool HasCurrentCheckoutContext(SurveyOrder order, SurveyResultReviewOperation intent) =>
        order.Scope == ContractScope.V3 &&
        order.Status == SurveyOrderStatus.AwaitingSettlement &&
        order.CurrentInvoiceRevisionId == intent.InvoiceRevisionId &&
        order.CurrentApprovedResultRevisionId == intent.ApprovedResultRevisionId &&
        order.VerifiedPoleCountSnapshotId == intent.PoleCountSnapshotId;

    private Task<SurveyResultReviewOperation?> FindOperationAsync(
        Guid tenantId, Guid actorId, Guid operationId, string action, CancellationToken cancellationToken) =>
        db.Set<SurveyResultReviewOperation>().AsNoTracking().SingleOrDefaultAsync(
            operation => operation.TenantId == tenantId && operation.ActorId == actorId &&
                operation.OperationId == operationId && operation.Action == action &&
                operation.ApiVersion == SurveyResultReviewOperation.ApiVersionV3,
            cancellationToken);

    private Result AddOperation<T>(
        SurveyOrder order, Guid actorId, Guid operationId, string action, string fingerprint, T outcome,
        DateTimeOffset occurredAt, Guid? invoiceId, Guid? approvalId, Guid? snapshotId)
    {
        using var document = JsonSerializer.SerializeToDocument(outcome);
        var operation = SurveyResultReviewOperation.Create(
            order.TenantId, order.FarmId, order.Id, actorId, action, SurveyResultReviewOperation.ApiVersionV3,
            operationId, fingerprint, document, occurredAt,
            approvedResultRevisionId: approvalId, poleCountSnapshotId: snapshotId, invoiceRevisionId: invoiceId);
        if (operation.IsFailure)
            return Result.Failure(operation.Error);

        db.Set<SurveyResultReviewOperation>().Add(operation.Value);
        return Result.Success();
    }

    private static T ReadOutcome<T>(SurveyResultReviewOperation operation) =>
        operation.Outcome.Deserialize<T>() ??
        throw new InvalidOperationException("A persisted checkout operation is missing its outcome.");

    private static string Fingerprint<T>(T input) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input)));

    private static AppError OperationConflict() => AppError.Conflict(
        "Checkout.OperationConflict", "The checkout operation identifier is already bound to a different request or intent.");

    private async Task<bool> HasActiveOwnerFarmAccessAsync(
        Guid tenantId,
        Guid farmId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return false;

        var matchedId = await db.Database.SqlQuery<Guid>($"""
            SELECT farm.id AS "Value"
            FROM identity.tenants tenant
            JOIN identity.users app_user
              ON app_user.id = {actorId}
             AND app_user.status = 'ACTIVE'::system.user_status
            JOIN identity.tenant_memberships membership
              ON membership.tenant_id = tenant.id
             AND membership.user_id = app_user.id
             AND membership.role = 'OWNER'::system.tenant_member_role
             AND membership.status = 'ACTIVE'::system.general_status
            JOIN farm.farms farm
              ON farm.id = {farmId}
             AND farm.tenant_id = tenant.id
             AND farm.status = 'ACTIVE'::system.general_status
             AND farm.deleted_at IS NULL
            WHERE tenant.id = {tenantId}
              AND tenant.status = 'ACTIVE'::system.general_status
            FOR SHARE OF tenant, app_user, membership, farm
            """).FirstOrDefaultAsync(cancellationToken);

        return matchedId == farmId;
    }
}
