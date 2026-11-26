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
    private const string SucceededPaymentEventType = "payment.succeeded";
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

            if (scope.TenantId != request.TenantId ||
                (request.ExpectedFarmId.HasValue && scope.FarmId != request.ExpectedFarmId.Value))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.Forbidden,
                    OrderId: request.OrderId,
                    ErrorCode: "Payment.Forbidden",
                    ErrorMessage: "Order does not belong to caller tenant or expected farm."));
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

            // ADR 0020 Rank 3: Idempotency replay by key and fingerprint before new intent checks
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

            // ADR 0020 Rank 5: Lock DepositObligation + all Order payments (FOR UPDATE) + Funding rows (FOR SHARE)
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

            var orderPayments = await db.Set<SurveyPayment>()
                .FromSqlInterpolated($"""
                    SELECT payment.*, payment.xmin FROM survey.survey_payments AS payment
                    WHERE payment.survey_order_id = {order.Id}
                    ORDER BY payment.id
                    FOR UPDATE
                    """)
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var fundingResult = await SurveyInvoiceFunding.ReadAsync(
                db,
                order,
                depositObligation,
                currentInvoiceRevisionId: order.CurrentInvoiceRevisionId,
                lockRows: true,
                cancellationToken,
                preloadedOrderPayments: orderPayments);
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

            var funding = fundingResult.Value;
            if (funding.HasOpenReconciliation)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                    OrderId: order.Id,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: invoice.Id,
                    InvoiceRevisionNumber: invoice.RevisionNumber,
                    InvoiceFingerprint: invoice.EvidenceFingerprint,
                    ErrorCode: SurveyInvoiceFunding.HoldErrorCode,
                    ErrorMessage: string.Join(" ", funding.ReconciliationReasons)));
            }

            if (funding.ActiveOtherRefundReservations > 0m)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                    OrderId: order.Id,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: invoice.Id,
                    InvoiceRevisionNumber: invoice.RevisionNumber,
                    InvoiceFingerprint: invoice.EvidenceFingerprint,
                    ErrorCode: "Settlement.ConflictingActiveRefundReservations",
                    ErrorMessage: "Order has active refund reservations for another origin and requires reconciliation before balance checkout."));
            }

            var dueAmount = funding.ComputeInvoiceSettlement(invoice.TotalAmount).DueAmount;

            // Check any FinalBalance checkout requiring reconciliation first, then open checkouts (Pending/Processing).
            // Prior Paid FinalBalance checkouts do NOT block creating a supplemental checkout when live dueAmount > 0.
            var balancePayments = orderPayments.Values
                .Where(p => p.Purpose == PaymentPurpose.FinalBalance)
                .OrderBy(p => p.Id)
                .ToList();

            var adjustmentPayment = balancePayments
                .FirstOrDefault(p => p.Status == SurveyPaymentStatus.AdjustmentRequired);
            if (adjustmentPayment is not null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                    OrderId: order.Id,
                    PaymentId: adjustmentPayment.Id,
                    OrderNumber: order.OrderNumber,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: adjustmentPayment.InvoiceRevisionId,
                    DueAmount: adjustmentPayment.Amount,
                    Currency: adjustmentPayment.Currency,
                    Provider: adjustmentPayment.Provider,
                    ExistingProviderReference: adjustmentPayment.ProviderReference,
                    IntentCreatedAt: adjustmentPayment.CreatedAt,
                    ErrorCode: "Checkout.AdjustmentRequired",
                    ErrorMessage: "An existing FinalBalance payment requires reconciliation before a new checkout can be created."));
            }

            var openBalancePayment = balancePayments
                .FirstOrDefault(p => p.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing);
            if (openBalancePayment is not null)
            {
                var hasUncertainOutcome = openBalancePayment.Status == SurveyPaymentStatus.Pending &&
                    await HasUncertainCheckoutOutcomeAsync(order.Id, openBalancePayment.Id, cancellationToken);
                var hasCallbackMoney = await HasCallbackMoneyRecordedAsync(order.Id, openBalancePayment.Id, cancellationToken);

                if (hasUncertainOutcome || hasCallbackMoney || string.IsNullOrWhiteSpace(openBalancePayment.ProviderReference))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Success(new V3BalanceCheckoutIntentResult(
                        Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                        OrderId: order.Id,
                        PaymentId: openBalancePayment.Id,
                        OrderNumber: order.OrderNumber,
                        OrderVersion: order.Version,
                        InvoiceRevisionId: openBalancePayment.InvoiceRevisionId,
                        DueAmount: openBalancePayment.Amount,
                        Currency: openBalancePayment.Currency,
                        Provider: openBalancePayment.Provider,
                        ExistingProviderReference: openBalancePayment.ProviderReference,
                        IntentCreatedAt: openBalancePayment.CreatedAt,
                        ErrorCode: "Checkout.UncertainProviderOutcome",
                        ErrorMessage: "An open FinalBalance checkout has an uncertain provider outcome or pending callback evidence and requires reconciliation."));
                }

                if (openBalancePayment.InvoiceRevisionId != invoice.Id ||
                    !string.Equals(openBalancePayment.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase) ||
                    openBalancePayment.Amount != dueAmount ||
                    !string.Equals(openBalancePayment.Provider, normalizedProvider, StringComparison.OrdinalIgnoreCase))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Success(new V3BalanceCheckoutIntentResult(
                        Outcome: V3BalanceCheckoutOutcome.ReconciliationHold,
                        OrderId: order.Id,
                        PaymentId: openBalancePayment.Id,
                        OrderNumber: order.OrderNumber,
                        OrderVersion: order.Version,
                        InvoiceRevisionId: openBalancePayment.InvoiceRevisionId,
                        DueAmount: openBalancePayment.Amount,
                        Currency: openBalancePayment.Currency,
                        Provider: openBalancePayment.Provider,
                        ExistingProviderReference: openBalancePayment.ProviderReference,
                        IntentCreatedAt: openBalancePayment.CreatedAt,
                        ErrorCode: "Checkout.OpenCheckoutMismatch",
                        ErrorMessage: $"Open FinalBalance checkout ({openBalancePayment.Amount} {openBalancePayment.Currency}) no longer matches current invoice ({invoice.Id}) or due ({dueAmount}); held for reconciliation."));
                }

                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ActiveCheckoutExists,
                    OrderId: order.Id,
                    PaymentId: openBalancePayment.Id,
                    OrderNumber: order.OrderNumber,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: openBalancePayment.InvoiceRevisionId,
                    InvoiceRevisionNumber: invoice.RevisionNumber,
                    InvoiceFingerprint: invoice.EvidenceFingerprint,
                    DueAmount: openBalancePayment.Amount,
                    Currency: openBalancePayment.Currency,
                    Provider: openBalancePayment.Provider,
                    ExistingProviderReference: openBalancePayment.ProviderReference,
                    IntentCreatedAt: openBalancePayment.CreatedAt,
                    ErrorCode: "Payment.ActivePaymentExists",
                    ErrorMessage: "An active FinalBalance checkout already exists for this order."));
            }

            if (dueAmount <= 0m)
            {
                var latestPaid = balancePayments
                    .LastOrDefault(p => p.Status == SurveyPaymentStatus.Paid);
                await tx.RollbackAsync(cancellationToken);
                return Result.Success(new V3BalanceCheckoutIntentResult(
                    Outcome: V3BalanceCheckoutOutcome.ZeroBalanceSettled,
                    OrderId: order.Id,
                    PaymentId: latestPaid?.Id,
                    OrderNumber: order.OrderNumber,
                    OrderVersion: order.Version,
                    InvoiceRevisionId: invoice.Id,
                    InvoiceRevisionNumber: invoice.RevisionNumber,
                    InvoiceFingerprint: invoice.EvidenceFingerprint,
                    DueAmount: 0m,
                    Currency: invoice.Currency,
                    Provider: latestPaid?.Provider,
                    ExistingProviderReference: latestPaid?.ProviderReference,
                    IntentCreatedAt: latestPaid?.CreatedAt,
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

            // ADR 0020 Rank 2 -> Rank 3 -> Rank 5: Lock Order FOR UPDATE before SurveyPayment FOR UPDATE
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

            var orderPayments = await db.Set<SurveyPayment>()
                .FromSqlInterpolated($"""
                    SELECT payment.*, payment.xmin FROM survey.survey_payments AS payment
                    WHERE payment.survey_order_id = {order.Id}
                    ORDER BY payment.id
                    FOR UPDATE
                    """)
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var payment = orderPayments.Values
                .SingleOrDefault(p => p.Id == request.PaymentId && p.Purpose == PaymentPurpose.FinalBalance);

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

            // Check whether a payment callback already recorded money BEFORE mutating payment status.
            var hasCallbackMoney = await HasCallbackMoneyRecordedAsync(order.Id, payment.Id, cancellationToken);
            if (hasCallbackMoney || payment.Status != SurveyPaymentStatus.Pending)
            {
                if (payment.Status is SurveyPaymentStatus.Failed or SurveyPaymentStatus.Processing or SurveyPaymentStatus.Refunded &&
                    !hasCallbackMoney)
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

                var (cbObligation, cbFundingResult, cbInvoice) = await LoadLiveFundingAndInvoiceAsync(
                    order, payment.InvoiceRevisionId, orderPayments, cancellationToken);

                if (payment.Status == SurveyPaymentStatus.Paid &&
                    cbObligation?.Status != SurveyDepositObligationStatus.Cancelled &&
                    cbFundingResult.IsSuccess &&
                    !cbFundingResult.Value.HasOpenReconciliation &&
                    cbFundingResult.Value.ActiveOtherRefundReservations == 0m &&
                    cbInvoice is { Status: SurveyInvoiceRevision.StatusCurrent } &&
                    cbFundingResult.Value.ComputeInvoiceSettlement(cbInvoice.TotalAmount).DueAmount == 0m)
                {
                    return await CommitOutcomeAsync(
                        V3BalanceCheckoutOutcome.ZeroBalanceSettled,
                        "Checkout.AlreadyPaidByCallback",
                        "Balance checkout was already settled by a verified payment callback.");
                }

                if (payment.Status == SurveyPaymentStatus.Pending && hasCallbackMoney)
                {
                    payment.RequireAdjustment(effectiveAt);
                }

                var cbHoldCode = payment.Status == SurveyPaymentStatus.Paid
                    ? "Checkout.PaidBalanceInsufficientForNewDue"
                    : "Checkout.CallbackRecordedBeforeOutcome";
                var cbHoldMessage = payment.Status == SurveyPaymentStatus.Paid
                    ? "A verified callback already marked this checkout Paid, but remaining due or reconciliation state requires review."
                    : "Callback payment evidence was recorded before checkout outcome completion; routed to reconciliation.";

                return await CommitOutcomeAsync(
                    V3BalanceCheckoutOutcome.ReconciliationHold,
                    cbHoldCode,
                    cbHoldMessage);
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

            // Re-verify live settlement funding (Rank 5) and current invoice (Rank 6) before marking Processing.
            var (outcomeObligation, liveFundingResult, liveInvoice) = await LoadLiveFundingAndInvoiceAsync(
                order, payment.InvoiceRevisionId, orderPayments, cancellationToken);

            string? holdCode = null;
            string? holdMessage = null;
            if (outcomeObligation?.Status == SurveyDepositObligationStatus.Cancelled)
            {
                holdCode = "DepositObligation.Invalid";
                holdMessage = "Order deposit obligation was cancelled before checkout completion.";
            }
            else if (liveFundingResult.IsFailure)
            {
                holdCode = liveFundingResult.Error.Code;
                holdMessage = liveFundingResult.Error.Description;
            }
            else if (liveFundingResult.Value.HasOpenReconciliation)
            {
                holdCode = SurveyInvoiceFunding.HoldErrorCode;
                holdMessage = string.Join(" ", liveFundingResult.Value.ReconciliationReasons);
            }
            else if (liveInvoice is null || liveInvoice.Status != SurveyInvoiceRevision.StatusCurrent)
            {
                holdCode = "Checkout.InvalidatedByCorrection";
                holdMessage = "The bound invoice revision is no longer current.";
            }
            else if (liveFundingResult.Value.ActiveOtherRefundReservations > 0m)
            {
                holdCode = "Settlement.ConflictingActiveRefundReservations";
                holdMessage = "Order has active refund reservations for another origin and requires reconciliation.";
            }
            else
            {
                var liveDueAmount = liveFundingResult.Value.ComputeInvoiceSettlement(liveInvoice.TotalAmount).DueAmount;
                if (liveDueAmount != payment.Amount)
                {
                    holdCode = "Checkout.DueAmountChanged";
                    holdMessage = $"Live remaining balance due ({liveDueAmount}) no longer matches checkout intent amount ({payment.Amount}); routed to reconciliation.";
                }
            }

            if (holdCode is not null)
            {
                payment.RequireAdjustment(effectiveAt);

                using var holdDoc = JsonSerializer.SerializeToDocument(new
                {
                    request.OperationId,
                    payment.Id,
                    payment.SurveyOrderId,
                    payment.InvoiceRevisionId,
                    payment.Amount,
                    payment.Currency,
                    payment.Provider,
                    payment.ProviderReference,
                    holdCode,
                    holdMessage,
                    paymentStatus = "ADJUSTMENT_REQUIRED"
                });
                db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    request.CorrelationId,
                    nameof(SurveyPayment),
                    payment.Id,
                    "V3_BALANCE_CHECKOUT_RECONCILIATION_HOLD",
                    null,
                    holdDoc,
                    effectiveAt));

                return await CommitOutcomeAsync(
                    V3BalanceCheckoutOutcome.ReconciliationHold,
                    holdCode,
                    holdMessage);
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

    private async Task<(
        SurveyDepositObligation? DepositObligation,
        Result<SurveyInvoiceFundingSummary> FundingResult,
        SurveyInvoiceRevision? Invoice)> LoadLiveFundingAndInvoiceAsync(
        SurveyOrder order,
        Guid? invoiceRevisionId,
        IReadOnlyDictionary<Guid, SurveyPayment> orderPayments,
        CancellationToken cancellationToken)
    {
        SurveyDepositObligation? obligation = null;
        if (order.DepositObligationId.HasValue)
        {
            obligation = await db.Set<SurveyDepositObligation>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_deposit_obligations WHERE id = {order.DepositObligationId.Value} AND survey_order_id = {order.Id} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        var fundingResult = obligation is null && order.DepositObligationId.HasValue
            ? Result.Failure<SurveyInvoiceFundingSummary>(AppError.Conflict("DepositObligation.Invalid", "Order deposit obligation is missing."))
            : await SurveyInvoiceFunding.ReadAsync(
                db,
                order,
                obligation,
                currentInvoiceRevisionId: invoiceRevisionId,
                lockRows: true,
                cancellationToken,
                preloadedOrderPayments: orderPayments);

        var invoice = invoiceRevisionId.HasValue
            ? await db.Set<SurveyInvoiceRevision>()
                .FromSqlInterpolated($"SELECT * FROM survey.survey_invoice_revisions WHERE id = {invoiceRevisionId.Value} AND survey_order_id = {order.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return (obligation, fundingResult, invoice);
    }

    private async Task<bool> HasUncertainCheckoutOutcomeAsync(
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var uncertainOps = await db.Set<SurveyResultReviewOperation>().AsNoTracking()
            .Where(op =>
                op.SurveyOrderId == orderId &&
                op.ApiVersion == SurveyResultReviewOperation.ApiVersionV3 &&
                op.Action == SurveyResultReviewOperation.ActionBalanceCheckoutUncertain)
            .ToListAsync(cancellationToken);

        return uncertainOps.Any(op =>
        {
            var outcome = op.Outcome.Deserialize<V3BalanceCheckoutResult>();
            return outcome?.PaymentId == paymentId;
        });
    }

    private async Task<bool> HasCallbackMoneyRecordedAsync(
        Guid orderId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var hasReceipt = await db.Set<SurveyFundingReceipt>().AsNoTracking()
            .AnyAsync(r => r.SurveyOrderId == orderId && r.SurveyPaymentId == paymentId, cancellationToken);
        if (hasReceipt)
            return true;

        return await db.Set<PaymentEvent>().AsNoTracking()
            .AnyAsync(e => e.SurveyPaymentId == paymentId && e.EventType == SucceededPaymentEventType, cancellationToken);
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
        Guid tenantId, Guid farmId, Guid actorId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return false;

        var matched = await db.Database.SqlQuery<Guid>($"""
            SELECT membership.id AS "Value"
            FROM identity.tenant_memberships membership
            JOIN identity.users app_user ON app_user.id = membership.user_id
            JOIN farm.farms farm ON farm.tenant_id = membership.tenant_id
            WHERE membership.tenant_id = {tenantId}
              AND membership.user_id = {actorId}
              AND membership.role = 'OWNER'::system.tenant_member_role
              AND membership.status = 'ACTIVE'::system.general_status
              AND app_user.status = 'ACTIVE'::system.user_status
              AND farm.id = {farmId}
              AND farm.status = 'ACTIVE'::system.general_status
              AND farm.deleted_at IS NULL
            FOR SHARE OF membership, app_user, farm
            """).FirstOrDefaultAsync(cancellationToken);

        return matched != Guid.Empty;
    }
}
