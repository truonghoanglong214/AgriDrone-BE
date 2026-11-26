using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class V3FundingReconciliationTransaction(
    AgriDroneSchemaDbContext db,
    OutboxMessageFactory outboxFactory,
    TimeProvider timeProvider) : IV3FundingReconciliationTransaction
{
    public async Task<Result<V3FundingReconciliationResult>> ReconcileAsync(
        V3FundingReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "V3 funding reconciliation must own its SQL transaction to preserve ADR 0020 lock ordering.");
        }

        if (request.OrderId == Guid.Empty ||
            request.ActorId == Guid.Empty ||
            request.CorrelationId == Guid.Empty ||
            request.OperationId == Guid.Empty ||
            (!request.SurveyPaymentId.HasValue && !request.FundingReceiptId.HasValue) ||
            request.ExpectedOrderVersion == 0u ||
            string.IsNullOrWhiteSpace(request.Reason) ||
            string.IsNullOrWhiteSpace(request.EvidenceReference))
        {
            return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                "Reconciliation.InvalidInputs",
                "OrderId, ActorId, OperationId, Reason, EvidenceReference, ExpectedOrderVersion, and a payment or receipt reference are required."));
        }

        if (request.SurveyPaymentId == Guid.Empty ||
            request.FundingReceiptId == Guid.Empty ||
            request.TargetInvoiceRevisionId == Guid.Empty)
        {
            return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                "Reconciliation.InvalidIdentifiers",
                "Target identifiers cannot be empty GUIDs."));
        }

        if (!Enum.IsDefined(request.DispositionKind))
        {
            return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                "Reconciliation.UnsupportedDisposition",
                "DispositionKind must be KeepExcluded, AllocateToCurrentInvoice, or RouteToRefund."));
        }

        if (request.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice &&
            !request.TargetInvoiceRevisionId.HasValue)
        {
            return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                "Reconciliation.TargetInvoiceRequired",
                "TargetInvoiceRevisionId is required when disposition is AllocateToCurrentInvoice."));
        }

        var normalizedCurrency = request.Currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!string.Equals(normalizedCurrency, "VND", StringComparison.Ordinal))
        {
            return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                "Reconciliation.CurrencyUnsupported",
                "Only VND currency is supported for V3 funding reconciliation."));
        }

        if (decimal.Truncate(request.Amount) != request.Amount ||
            (request.DispositionKind == SurveyReconciliationDispositionKind.KeepExcluded
                ? request.Amount < 0m
                : request.Amount <= 0m))
        {
            return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                "Reconciliation.AmountInvalid",
                "Reconciliation amount must be a valid whole VND amount."));
        }

        var normalizedReason = request.Reason.Trim();
        var normalizedEvidence = request.EvidenceReference.Trim();
        var normalizedProviderRef = string.IsNullOrWhiteSpace(request.ProviderReference) ? null : request.ProviderReference.Trim();
        var normalizedTxId = string.IsNullOrWhiteSpace(request.ProviderTransactionId) ? null : request.ProviderTransactionId.Trim();
        var payloadFingerprint = BuildPayloadFingerprint(
            request.OrderId,
            request.OperationId,
            request.DispositionKind,
            request.SurveyPaymentId,
            request.FundingReceiptId,
            request.TargetInvoiceRevisionId,
            request.Amount,
            normalizedCurrency,
            normalizedProviderRef,
            normalizedTxId,
            normalizedReason,
            normalizedEvidence);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            // ADR 0020 Rank 1: Verify active SYSTEM_ADMIN identity BEFORE locking SurveyOrder or financial rows.
            if (!await IsActiveSystemAdminAsync(request.ActorId, cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3FundingReconciliationResult>(PaymentError.Forbidden());
            }

            // ADR 0020 Rank 2: Lock SurveyOrder FOR UPDATE.
            var order = await db.Set<SurveyOrder>()
                .FromSqlInterpolated($"SELECT o.*, o.xmin FROM survey.survey_orders AS o WHERE o.id = {request.OrderId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);

            if (order is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3FundingReconciliationResult>(SurveyOrderError.NotFound(request.OrderId));
            }

            if (order.Scope != ContractScope.V3)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                    "Reconciliation.LegacyOrderNotSupported",
                    "V3 funding reconciliation is only available for V3 orders."));
            }

            // ADR 0020 Rank 3: Idempotency lookup by (SurveyOrderId, OperationId).
            var existingDecision = await db.Set<SurveyFundingReconciliationDecision>()
                .FromSqlInterpolated($"""
                    SELECT d.* FROM survey.survey_funding_reconciliation_decisions AS d
                    WHERE d.survey_order_id = {order.Id} AND d.operation_id = {request.OperationId}
                    FOR SHARE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

            if (existingDecision is not null)
            {
                if (!string.Equals(existingDecision.PayloadFingerprint, payloadFingerprint, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                        "Reconciliation.OperationIdConflict",
                        "OperationId was already used with a different reconciliation payload."));
                }

                SurveyPayment? existingPayment = null;
                if (existingDecision.SurveyPaymentId.HasValue)
                {
                    existingPayment = await db.Set<SurveyPayment>().AsNoTracking()
                        .SingleOrDefaultAsync(p => p.Id == existingDecision.SurveyPaymentId.Value && p.SurveyOrderId == order.Id, cancellationToken);
                }

                SurveyDepositObligation? replayObligation = null;
                if (order.DepositObligationId.HasValue)
                {
                    replayObligation = await db.Set<SurveyDepositObligation>().AsNoTracking()
                        .SingleOrDefaultAsync(o => o.Id == order.DepositObligationId.Value && o.SurveyOrderId == order.Id, cancellationToken);
                }

                var replayFunding = await SurveyInvoiceFunding.ReadAsync(
                    db, order, replayObligation, order.CurrentInvoiceRevisionId, lockRows: false, cancellationToken);

                await tx.CommitAsync(cancellationToken);
                return Result.Success(new V3FundingReconciliationResult(
                    DecisionId: existingDecision.Id,
                    OrderId: order.Id,
                    OperationId: existingDecision.OperationId,
                    DispositionKind: existingDecision.DispositionKind,
                    SurveyPaymentId: existingDecision.SurveyPaymentId,
                    FundingReceiptId: existingDecision.FundingReceiptId,
                    TargetInvoiceRevisionId: existingDecision.TargetInvoiceRevisionId,
                    Amount: existingDecision.Amount,
                    Currency: existingDecision.Currency,
                    ProviderReference: existingDecision.ProviderReference,
                    ProviderTransactionId: existingDecision.ProviderTransactionId,
                    PaymentStatus: existingPayment is null ? null : existingPayment.Status.ToString().ToUpperInvariant(),
                    OrderStatus: order.Status.ToString().ToUpperInvariant(),
                    PaymentVersion: existingPayment?.Version,
                    OrderVersion: order.Version,
                    HasOpenReconciliation: replayFunding.IsFailure || replayFunding.Value.HasOpenReconciliation,
                    ConfirmedNetReceipts: replayFunding.IsSuccess ? replayFunding.Value.ConfirmedNetReceipts : 0m,
                    OrderConfirmedNetCash: replayFunding.IsSuccess ? replayFunding.Value.OrderConfirmedNetCash : 0m,
                    AvailableRefundCap: replayFunding.IsSuccess ? replayFunding.Value.AvailableRefundCap : 0m,
                    ReconciledBy: existingDecision.ReconciledBy,
                    ReconciledAt: existingDecision.ReconciledAt,
                    IsReplay: true));
            }

            if (order.Version != request.ExpectedOrderVersion)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3FundingReconciliationResult>(SurveyOrderError.VersionConflict());
            }

            // ADR 0020 Rank 5: Lock DepositObligation + all SurveyPayments + read funding rows FOR SHARE.
            SurveyDepositObligation? depositObligation = null;
            if (order.DepositObligationId.HasValue)
            {
                depositObligation = await db.Set<SurveyDepositObligation>()
                    .FromSqlInterpolated($"SELECT o.* FROM survey.survey_deposit_obligations AS o WHERE o.id = {order.DepositObligationId.Value} AND o.survey_order_id = {order.Id} FOR SHARE")
                    .SingleOrDefaultAsync(cancellationToken);
            }

            var orderPayments = await db.Set<SurveyPayment>()
                .FromSqlInterpolated($"SELECT p.*, p.xmin FROM survey.survey_payments AS p WHERE p.survey_order_id = {order.Id} ORDER BY p.id FOR UPDATE")
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var preFundingResult = await SurveyInvoiceFunding.ReadAsync(
                db, order, depositObligation, order.CurrentInvoiceRevisionId, lockRows: true, cancellationToken, preloadedOrderPayments: orderPayments);
            if (preFundingResult.IsFailure)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3FundingReconciliationResult>(preFundingResult.Error);
            }

            var preFunding = preFundingResult.Value;

            SurveyFundingReceipt? receipt = null;
            SurveyReceiptFundingEntry? receiptEntry = null;
            if (request.FundingReceiptId.HasValue)
            {
                receiptEntry = preFunding.Receipts.SingleOrDefault(r => r.Receipt.Id == request.FundingReceiptId.Value);
                if (receiptEntry is null)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.NotFound(
                        "Reconciliation.ReceiptNotFound",
                        $"Funding receipt '{request.FundingReceiptId.Value}' was not found for this order."));
                }

                receipt = receiptEntry.Receipt;
            }

            var resolvedPaymentId = request.SurveyPaymentId ?? receipt?.SurveyPaymentId;
            SurveyPayment? payment = null;
            if (resolvedPaymentId.HasValue)
            {
                if (!orderPayments.TryGetValue(resolvedPaymentId.Value, out payment))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(
                        PaymentError.NotFound(resolvedPaymentId.Value.ToString("D")));
                }

                if (receipt is not null && receipt.SurveyPaymentId.HasValue && receipt.SurveyPaymentId.Value != payment.Id)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(PaymentError.ReconciliationMismatch());
                }

                if (request.ExpectedPaymentVersion.HasValue && payment.Version != request.ExpectedPaymentVersion.Value)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(PaymentError.VersionConflict());
                }

                if (normalizedProviderRef is not null &&
                    payment.ProviderReference is not null &&
                    !string.Equals(payment.ProviderReference, normalizedProviderRef, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(PaymentError.ReconciliationMismatch());
                }
            }

            if (receipt is not null)
            {
                if (normalizedProviderRef is not null &&
                    !string.Equals(receipt.PaymentReference, normalizedProviderRef, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(PaymentError.ReconciliationMismatch());
                }

                var hasFinalDisposition = await db.Set<SurveyFundingReconciliationDecision>()
                    .AnyAsync(decision =>
                        decision.SurveyOrderId == order.Id &&
                        decision.FundingReceiptId == receipt.Id &&
                        decision.DispositionKind != SurveyReconciliationDispositionKind.KeepExcluded,
                        cancellationToken);
                if (hasFinalDisposition)
                {
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                        "Reconciliation.ReceiptAlreadyResolved",
                        "This funding receipt already has a final reconciliation disposition."));
                }

                if (request.Amount > receipt.Amount)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                        "Reconciliation.AmountExceedsReceipt",
                        "Reconciliation amount cannot exceed the funding receipt amount."));
                }

                var isUnverifiedMissingTx = receipt.ProviderTransactionId.StartsWith("unverified-tx:", StringComparison.Ordinal);
                if (request.DispositionKind != SurveyReconciliationDispositionKind.KeepExcluded &&
                    isUnverifiedMissingTx &&
                    string.IsNullOrWhiteSpace(normalizedTxId))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                        "Reconciliation.ProviderTransactionIdRequired",
                        "A verified ProviderTransactionId is required before allocating or routing an unverified callback to refund."));
                }

                if (normalizedTxId is not null &&
                    (normalizedTxId.StartsWith("unverified-tx:", StringComparison.OrdinalIgnoreCase) ||
                     (!isUnverifiedMissingTx &&
                      !string.Equals(normalizedTxId, receipt.ProviderTransactionId, StringComparison.Ordinal))))
                {
                    return Result.Failure<V3FundingReconciliationResult>(PaymentError.ReconciliationMismatch());
                }

                if (request.DispositionKind != SurveyReconciliationDispositionKind.KeepExcluded)
                {
                    var verifiedTxId = normalizedTxId ?? (isUnverifiedMissingTx ? null : receipt.ProviderTransactionId);
                    if (!string.IsNullOrWhiteSpace(verifiedTxId) &&
                        await HasDuplicateProviderTransactionAsync(
                            receipt.Provider,
                            verifiedTxId,
                            receipt.Id,
                            cancellationToken))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                            "Reconciliation.DuplicateProviderTransactionId",
                            $"Provider transaction '{verifiedTxId}' is already recorded on another funding receipt or reconciliation decision. Keep this unverified entry excluded (`KeepExcluded`)."));
                    }
                }

                if (request.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice &&
                    receiptEntry!.Kind == SurveyFundingReceiptKind.OrderDeposit)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                        "Reconciliation.DepositCannotAllocateToInvoice",
                        "OrderDeposit receipts cannot be allocated to a balance invoice via reconciliation."));
                }

                if (request.DispositionKind == SurveyReconciliationDispositionKind.RouteToRefund)
                {
                    var maxAvailableOnReceipt = isUnverifiedMissingTx
                        ? receipt.Amount - receiptEntry!.ReversedCashAmount - receiptEntry.CompletedRefundsAmount - receiptEntry.ActiveReservationsAmount - receiptEntry.RoutedToRefundAmount
                        : receiptEntry!.AvailableCashAmount;
                    if (request.Amount > maxAvailableOnReceipt ||
                        (!isUnverifiedMissingTx && request.Amount > preFunding.AvailableRefundCap))
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                            "Reconciliation.ExceedsAvailableRefundCap",
                            "Routed refund amount exceeds remaining available cash or refund cap on the receipt/order."));
                    }
                }
            }
            else if (request.DispositionKind != SurveyReconciliationDispositionKind.KeepExcluded)
            {
                return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                    "Reconciliation.ReceiptRequired",
                    "A funding receipt is required to allocate funds or route them to refund."));
            }

            // ADR 0020 Rank 6: Lock current invoice FOR SHARE when allocating to current invoice.
            SurveyInvoiceRevision? targetInvoice = null;
            if (request.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice)
            {
                if (order.Status != SurveyOrderStatus.AwaitingSettlement ||
                    order.CurrentInvoiceRevisionId != request.TargetInvoiceRevisionId!.Value)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                        "Reconciliation.TargetInvoiceNotCurrent",
                        "TargetInvoiceRevisionId must match the order's current invoice in AwaitingSettlement."));
                }

                targetInvoice = await db.Set<SurveyInvoiceRevision>()
                    .FromSqlInterpolated($"SELECT i.* FROM survey.survey_invoice_revisions AS i WHERE i.id = {order.CurrentInvoiceRevisionId.Value} AND i.survey_order_id = {order.Id} AND i.tenant_id = {order.TenantId} AND i.farm_id = {order.FarmId} FOR SHARE")
                    .SingleOrDefaultAsync(cancellationToken);

                if (targetInvoice is null ||
                    targetInvoice.Status != SurveyInvoiceRevision.StatusCurrent ||
                    !string.Equals(targetInvoice.Currency, normalizedCurrency, StringComparison.Ordinal))
                {
                    await tx.RollbackAsync(cancellationToken);
                    return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(
                        "Reconciliation.TargetInvoiceNotCurrent",
                        "Target invoice revision is no longer current or currency does not match."));
                }
            }

            var reconciledAt = timeProvider.GetUtcNow();
            if (payment is not null)
            {
                var effectiveAt = reconciledAt >= payment.UpdatedAt ? reconciledAt : payment.UpdatedAt;
                var eligibleForPayment = preFunding.Receipts
                    .Where(entry => entry.Receipt.SurveyPaymentId == payment.Id &&
                                    entry.Receipt.Id != receipt?.Id)
                    .Sum(entry => entry.EligibleSettlementAmount);

                if (request.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice &&
                    payment.InvoiceRevisionId == targetInvoice?.Id)
                {
                    eligibleForPayment += request.Amount;
                }

                var resolvesPayment =
                    request.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice ||
                    request.DispositionKind == SurveyReconciliationDispositionKind.RouteToRefund;
                if (resolvesPayment && payment.Purpose == PaymentPurpose.FinalBalance &&
                    eligibleForPayment >= payment.Amount &&
                    (payment.Status is SurveyPaymentStatus.Pending or SurveyPaymentStatus.Processing or SurveyPaymentStatus.AdjustmentRequired))
                {
                    var effectiveRef = payment.ProviderReference ?? receipt?.PaymentReference;
                    if (string.IsNullOrWhiteSpace(effectiveRef))
                    {
                        return Result.Failure<V3FundingReconciliationResult>(AppError.Validation(
                            "Reconciliation.ProviderReferenceRequired",
                            "ProviderReference is required to reconcile a payment into Paid."));
                    }

                    payment.Reconcile(
                        Money.Create(payment.Amount, CurrencyCode.Create(payment.Currency)),
                        effectiveRef,
                        effectiveAt);
                }
            }

            var decision = SurveyFundingReconciliationDecision.Create(
                id: Guid.CreateVersion7(),
                tenantId: order.TenantId,
                surveyOrderId: order.Id,
                operationId: request.OperationId,
                payloadFingerprint: payloadFingerprint,
                dispositionKind: request.DispositionKind,
                surveyPaymentId: payment?.Id,
                fundingReceiptId: receipt?.Id,
                targetInvoiceRevisionId: request.TargetInvoiceRevisionId,
                amount: request.Amount,
                currency: normalizedCurrency,
                providerReference: normalizedProviderRef ?? receipt?.PaymentReference ?? payment?.ProviderReference,
                providerTransactionId: normalizedTxId ?? receipt?.ProviderTransactionId,
                reason: normalizedReason,
                evidenceReference: normalizedEvidence,
                expectedOrderVersion: order.Version,
                reconciledBy: request.ActorId,
                reconciledAt: reconciledAt,
                createdAt: reconciledAt);

            db.Set<SurveyFundingReconciliationDecision>().Add(decision);

            using var newAuditDoc = JsonSerializer.SerializeToDocument(new
            {
                decision.Id,
                decision.OperationId,
                dispositionKind = decision.DispositionKind.ToString(),
                decision.SurveyPaymentId,
                decision.FundingReceiptId,
                decision.TargetInvoiceRevisionId,
                decision.Amount,
                decision.Currency,
                decision.ProviderReference,
                decision.ProviderTransactionId,
                decision.EvidenceReference,
                decision.Reason,
                paymentStatus = payment?.Status.ToString().ToUpperInvariant(),
                orderStatus = order.Status.ToString()
            });
            db.Set<AuditLog>().Add(AuditLog.ForUserAction(
                order.TenantId,
                order.FarmId,
                request.ActorId,
                request.CorrelationId,
                nameof(SurveyFundingReconciliationDecision),
                decision.Id,
                "RECONCILE_V3_SURVEY_FUNDING",
                null,
                newAuditDoc,
                reconciledAt));

            if (request.DispositionKind == SurveyReconciliationDispositionKind.AllocateToCurrentInvoice &&
                order.Status == SurveyOrderStatus.AwaitingSettlement &&
                targetInvoice is not null)
            {
                var trigger = new SurveyReleaseEvaluationRequestedV1(
                    OrderId: order.Id,
                    TenantId: order.TenantId,
                    FarmId: order.FarmId,
                    TriggerSource: SurveyReleaseTriggerSource.AdminReconciliation.ToString(),
                    OperationId: decision.OperationId,
                    PayloadFingerprint: decision.PayloadFingerprint,
                    RequestedAt: reconciledAt,
                    ExpectedOrderVersion: order.Version,
                    ExpectedApprovedResultRevisionId: targetInvoice.ApprovedResultRevisionId,
                    ExpectedCountSnapshotId: targetInvoice.PoleCountSnapshotId,
                    ExpectedInvoiceRevisionId: targetInvoice.Id,
                    ExpectedInvoiceRevisionNumber: targetInvoice.RevisionNumber,
                    ExpectedInvoiceFingerprint: targetInvoice.EvidenceFingerprint,
                    TriggerActorId: request.ActorId,
                    TriggerPaymentId: payment?.Id,
                    TriggerFundingReceiptId: receipt?.Id);

                var triggerEnvelope = IntegrationEventEnvelopeFactory.Create(
                    IntegrationEventDescriptors.SurveyReleaseEvaluationRequestedV1,
                    Guid.CreateVersion7(),
                    request.CorrelationId,
                    order.TenantId,
                    request.ActorId,
                    reconciledAt,
                    trigger);
                db.Set<SharedInfrastructure.Messaging.Persistence.OutboxMessage>().Add(
                    outboxFactory.Create(
                        triggerEnvelope,
                        IntegrationEventTypes.SurveyReleaseEvaluationRequestedV1,
                        order.Id.ToString("D")));
            }

            await db.SaveChangesAsync(cancellationToken);

            var postFundingResult = await SurveyInvoiceFunding.ReadAsync(
                db, order, depositObligation, order.CurrentInvoiceRevisionId, lockRows: false, cancellationToken, preloadedOrderPayments: orderPayments);
            if (postFundingResult.IsFailure)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result.Failure<V3FundingReconciliationResult>(postFundingResult.Error);
            }

            var postFunding = postFundingResult.Value;
            await tx.CommitAsync(cancellationToken);

            return Result.Success(new V3FundingReconciliationResult(
                DecisionId: decision.Id,
                OrderId: order.Id,
                OperationId: decision.OperationId,
                DispositionKind: decision.DispositionKind,
                SurveyPaymentId: decision.SurveyPaymentId,
                FundingReceiptId: decision.FundingReceiptId,
                TargetInvoiceRevisionId: decision.TargetInvoiceRevisionId,
                Amount: decision.Amount,
                Currency: decision.Currency,
                ProviderReference: decision.ProviderReference,
                ProviderTransactionId: decision.ProviderTransactionId,
                PaymentStatus: payment?.Status.ToString().ToUpperInvariant(),
                OrderStatus: order.Status.ToString().ToUpperInvariant(),
                PaymentVersion: payment?.Version,
                OrderVersion: order.Version,
                HasOpenReconciliation: postFunding.HasOpenReconciliation,
                ConfirmedNetReceipts: postFunding.ConfirmedNetReceipts,
                OrderConfirmedNetCash: postFunding.OrderConfirmedNetCash,
                AvailableRefundCap: postFunding.AvailableRefundCap,
                ReconciledBy: decision.ReconciledBy,
                ReconciledAt: decision.ReconciledAt,
                IsReplay: false));
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3FundingReconciliationResult>(PaymentError.PersistenceConflict());
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            pg.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3FundingReconciliationResult>(PaymentError.PersistenceConflict());
        }
        catch (SurveyDomainException ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return Result.Failure<V3FundingReconciliationResult>(AppError.Conflict(ex.Code, ex.Message));
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<bool> IsActiveSystemAdminAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return false;
        }

        var matched = await db.Database.SqlQuery<Guid>($"""
            SELECT app_user.id AS "Value"
            FROM identity.users app_user
            JOIN identity.user_roles user_role ON user_role.user_id = app_user.id
            JOIN identity.roles role ON role.id = user_role.role_id
            WHERE app_user.id = {actorId}
              AND app_user.status = 'ACTIVE'::system.user_status
              AND role.code = 'SYSTEM_ADMIN'
            FOR SHARE OF app_user, user_role, role
            """).FirstOrDefaultAsync(cancellationToken);

        return matched == actorId;
    }

    private async Task<bool> HasDuplicateProviderTransactionAsync(
        string provider,
        string providerTransactionId,
        Guid currentReceiptId,
        CancellationToken cancellationToken)
    {
        var normalizedProvider = provider.Trim().ToLowerInvariant();
        var duplicateReceiptExists = await db.Database.SqlQuery<Guid>($"""
            SELECT receipt.id AS "Value"
            FROM survey.survey_funding_receipts AS receipt
            WHERE LOWER(receipt.provider) = {normalizedProvider}
              AND receipt.provider_transaction_id = {providerTransactionId}
              AND receipt.id <> {currentReceiptId}
            LIMIT 1
            """).AnyAsync(cancellationToken);

        if (duplicateReceiptExists)
        {
            return true;
        }

        return await db.Database.SqlQuery<Guid>($"""
            SELECT decision.id AS "Value"
            FROM survey.survey_funding_reconciliation_decisions AS decision
            LEFT JOIN survey.survey_funding_receipts AS receipt
              ON receipt.id = decision.funding_receipt_id
             AND receipt.survey_order_id = decision.survey_order_id
            LEFT JOIN survey.survey_payments AS payment
              ON payment.id = decision.survey_payment_id
             AND payment.survey_order_id = decision.survey_order_id
            WHERE decision.provider_transaction_id = {providerTransactionId}
              AND decision.disposition_kind IN ('AllocateToCurrentInvoice', 'RouteToRefund')
              AND (decision.funding_receipt_id IS NULL OR decision.funding_receipt_id <> {currentReceiptId})
              AND LOWER(COALESCE(receipt.provider, payment.provider, '')) = {normalizedProvider}
            LIMIT 1
            """).AnyAsync(cancellationToken);
    }

    private static string BuildPayloadFingerprint(
        Guid orderId,
        Guid operationId,
        SurveyReconciliationDispositionKind dispositionKind,
        Guid? surveyPaymentId,
        Guid? fundingReceiptId,
        Guid? targetInvoiceRevisionId,
        decimal amount,
        string currency,
        string? providerReference,
        string? providerTransactionId,
        string reason,
        string evidenceReference)
    {
        var canonical = string.Join(
            '|',
            "v3-reconcile-v1",
            orderId.ToString("D"),
            operationId.ToString("D"),
            dispositionKind.ToString(),
            surveyPaymentId?.ToString("D") ?? string.Empty,
            fundingReceiptId?.ToString("D") ?? string.Empty,
            targetInvoiceRevisionId?.ToString("D") ?? string.Empty,
            amount.ToString("0.00", CultureInfo.InvariantCulture),
            currency,
            providerReference ?? string.Empty,
            providerTransactionId ?? string.Empty,
            reason,
            evidenceReference);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
