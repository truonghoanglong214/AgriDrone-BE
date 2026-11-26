using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal enum SurveyFundingReceiptKind
{
    OrderDeposit = 1,
    FinalBalance = 2
}

internal sealed record SurveyReceiptFundingEntry(
    SurveyFundingReceipt Receipt,
    SurveyFundingReceiptKind Kind,
    bool IsEligibleForCurrentInvoiceSettlement,
    string? ExclusionReason,
    decimal AllocatedDepositAmount,
    decimal UnallocatedAmount,
    decimal ReversedCashAmount,
    decimal ReversedAllocationAmount,
    decimal CompletedRefundsAmount,
    decimal ActiveReservationsAmount,
    decimal AvailableCashAmount,
    bool HasAnyRefundObligationRow,
    decimal RoutedToRefundAmount,
    decimal EligibleSettlementAmount);

internal sealed record SurveyInvoiceSettlementSummary(
    decimal InvoiceTotalAmount,
    decimal ConfirmedNetPaid,
    decimal DueAmount,
    decimal ExcessAmount,
    decimal ActiveOverpaymentReservations,
    decimal RemainingExcessToReserve);

internal sealed record SurveyOverpaymentReceiptAllocation(
    Guid FundingReceiptId,
    decimal Amount,
    decimal AvailableCashBeforeReserve);

internal sealed record SurveyOverpaymentReservationPlan(
    Guid OriginDecisionId,
    string Currency,
    decimal ExcessAmount,
    decimal AlreadyReservedAmount,
    decimal PlannedAmount,
    IReadOnlyList<SurveyRefundObligation> ExistingObligations,
    IReadOnlyList<SurveyOverpaymentReceiptAllocation> Allocations)
{
    internal IReadOnlyList<SurveyRefundObligation> CreateObligations(
        Guid surveyOrderId,
        Guid tenantId,
        DateTimeOffset reservedAt)
    {
        if (Allocations.Count == 0)
            return [];

        var created = new List<SurveyRefundObligation>(Allocations.Count);
        foreach (var allocation in Allocations)
        {
            var obligation = SurveyRefundObligation.Create(
                id: Guid.CreateVersion7(),
                surveyOrderId: surveyOrderId,
                tenantId: tenantId,
                originKind: SurveyRefundOriginKind.Overpayment,
                originDecisionId: OriginDecisionId,
                amount: allocation.Amount,
                currency: Currency,
                createdAt: reservedAt,
                fundingReceiptId: allocation.FundingReceiptId);
            obligation.MarkReserved(reservedAt);
            created.Add(obligation);
        }

        return created;
    }
}

internal sealed record SurveyInvoiceFundingSummary(
    decimal ValidAllocatedDepositNet,
    decimal ConfirmedNetReceipts,
    decimal EligibleGrossReceipts,
    decimal EligibleCashReversals,
    decimal EligibleCompletedRefunds,
    decimal GrossConfirmedReceipts,
    decimal TotalCashReversals,
    decimal TotalCompletedRefunds,
    decimal TotalActiveReservations,
    decimal ActiveOverpaymentReservations,
    decimal ActiveOtherRefundReservations,
    decimal OrderConfirmedNetCash,
    decimal AvailableRefundCap,
    bool HasUnlinkedRefundObligations,
    bool HasOpenReconciliation,
    IReadOnlyList<string> ReconciliationReasons,
    IReadOnlyList<Guid> ExcludedReceiptIds,
    IReadOnlyList<SurveyReceiptFundingEntry> Receipts,
    IReadOnlyList<Guid> ReceiptIds,
    IReadOnlyList<Guid> ReversalIds,
    IReadOnlyList<Guid> ActiveOrCompletedRefundIds,
    IReadOnlyList<SurveyRefundObligation> ActiveOverpaymentObligations,
    decimal RoutedToRefundAmount = 0m,
    IReadOnlyList<Guid>? ReconciliationDecisionIds = null)
{
    internal decimal ConfirmedNetPaid => ConfirmedNetReceipts;

    internal SurveyInvoiceSettlementSummary ComputeInvoiceSettlement(decimal invoiceTotalAmount)
    {
        var due = Math.Max(0m, invoiceTotalAmount - ConfirmedNetPaid);
        var excess = Math.Max(0m, ConfirmedNetPaid - invoiceTotalAmount);
        var remainingToReserve = Math.Max(0m, excess - ActiveOverpaymentReservations);
        return new SurveyInvoiceSettlementSummary(
            InvoiceTotalAmount: invoiceTotalAmount,
            ConfirmedNetPaid: ConfirmedNetPaid,
            DueAmount: due,
            ExcessAmount: excess,
            ActiveOverpaymentReservations: ActiveOverpaymentReservations,
            RemainingExcessToReserve: remainingToReserve);
    }

    internal Result<SurveyOverpaymentReservationPlan> PlanOverpaymentReservations(
        decimal invoiceTotalAmount,
        Guid originDecisionId,
        string currency)
    {
        if (originDecisionId == Guid.Empty)
        {
            return Result.Failure<SurveyOverpaymentReservationPlan>(AppError.Validation(
                "OverpaymentReservation.OriginDecisionRequired",
                "A non-empty origin decision identifier is required to plan overpayment reservations."));
        }

        if (HasOpenReconciliation)
        {
            return Result.Failure<SurveyOverpaymentReservationPlan>(AppError.Conflict(
                SurveyInvoiceFunding.HoldErrorCode,
                string.Join(" ", ReconciliationReasons)));
        }

        if (ActiveOtherRefundReservations > 0m)
        {
            return Result.Failure<SurveyOverpaymentReservationPlan>(AppError.Conflict(
                "SettlementRelease.ConflictingActiveRefundReservations",
                "Active refund reservations for another refund origin block overpayment reservation until reconciled."));
        }

        var settlement = ComputeInvoiceSettlement(invoiceTotalAmount);
        if (ActiveOverpaymentReservations > settlement.ExcessAmount)
        {
            return Result.Failure<SurveyOverpaymentReservationPlan>(AppError.Conflict(
                "SettlementRelease.OverpaymentReservationExceedsExcess",
                "Existing active overpayment reservations exceed current invoice excess and require reconciliation."));
        }

        var matchingDecisionObligations = ActiveOverpaymentObligations
            .Where(o => o.OriginDecisionId == originDecisionId)
            .OrderBy(o => o.CreatedAt)
            .ThenBy(o => o.Id)
            .ToList();

        if (ActiveOverpaymentObligations.Any(o => o.OriginDecisionId != originDecisionId))
        {
            return Result.Failure<SurveyOverpaymentReservationPlan>(AppError.Conflict(
                "SettlementRelease.ConflictingOverpaymentDecision",
                "Active overpayment reservations exist under a different origin decision and require reconciliation."));
        }

        if (settlement.RemainingExcessToReserve == 0m)
        {
            return Result.Success(new SurveyOverpaymentReservationPlan(
                OriginDecisionId: originDecisionId,
                Currency: currency,
                ExcessAmount: settlement.ExcessAmount,
                AlreadyReservedAmount: ActiveOverpaymentReservations,
                PlannedAmount: 0m,
                ExistingObligations: matchingDecisionObligations,
                Allocations: []));
        }

        // Filter BEFORE allocating: only eligible settlement receipts with available cash and no existing
        // row in survey_refund_obligations (including Cancelled/Completed rows occupying uq_survey_refund_obligations_late_receipt).
        var remaining = settlement.RemainingExcessToReserve;
        var allocations = new List<SurveyOverpaymentReceiptAllocation>();
        foreach (var entry in Receipts)
        {
            if (!entry.IsEligibleForCurrentInvoiceSettlement ||
                entry.AvailableCashAmount <= 0m ||
                entry.HasAnyRefundObligationRow)
            {
                continue;
            }

            var take = Math.Min(remaining, entry.AvailableCashAmount);
            allocations.Add(new SurveyOverpaymentReceiptAllocation(
                FundingReceiptId: entry.Receipt.Id,
                Amount: take,
                AvailableCashBeforeReserve: entry.AvailableCashAmount));
            remaining -= take;
            if (remaining == 0m)
                break;
        }

        if (remaining > 0m)
        {
            return Result.Failure<SurveyOverpaymentReservationPlan>(AppError.Conflict(
                "SettlementRelease.InsufficientEligibleReceiptCapacityForExcess",
                $"Eligible unencumbered receipts without existing refund obligation rows cannot cover remaining excess ({settlement.RemainingExcessToReserve})."));
        }

        return Result.Success(new SurveyOverpaymentReservationPlan(
            OriginDecisionId: originDecisionId,
            Currency: currency,
            ExcessAmount: settlement.ExcessAmount,
            AlreadyReservedAmount: ActiveOverpaymentReservations,
            PlannedAmount: settlement.RemainingExcessToReserve,
            ExistingObligations: matchingDecisionObligations,
            Allocations: allocations));
    }
}

internal static class SurveyInvoiceFunding
{
    internal const string HoldErrorCode = "InvoiceFinalization.RefundReconciliationHold";

    // R05E-02 / A2.2: Unified per-order, per-receipt funding ledger.
    // Separates current-invoice settlement (eligible receipts), Order-level cash/refund cap,
    // and open reconciliation status so GET invoice can compute current due while writers fail closed.
    internal static async Task<Result<SurveyInvoiceFundingSummary>> ReadAsync(
        AgriDroneSchemaDbContext db,
        SurveyOrder order,
        SurveyDepositObligation? depositObligation,
        Guid? currentInvoiceRevisionId,
        bool lockRows,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<Guid, SurveyPayment>? preloadedOrderPayments = null,
        bool allowHistoricalUnlinkedRefunds = false)
    {
        var useRelationalLock = lockRows && db.Database.IsRelational();

        IReadOnlyDictionary<Guid, SurveyPayment> orderPayments;
        if (preloadedOrderPayments is not null)
        {
            orderPayments = preloadedOrderPayments;
        }
        else
        {
            var paymentList = await (useRelationalLock
                ? db.Set<SurveyPayment>().FromSqlInterpolated(
                    $"SELECT p.*, p.xmin FROM survey.survey_payments AS p WHERE p.survey_order_id = {order.Id} ORDER BY p.id FOR SHARE")
                : db.Set<SurveyPayment>().AsNoTracking()
                    .Where(row => row.SurveyOrderId == order.Id)
                    .OrderBy(row => row.Id))
                .ToListAsync(cancellationToken);
            orderPayments = paymentList.ToDictionary(p => p.Id);
        }

        var receipts = await (useRelationalLock
            ? db.Set<SurveyFundingReceipt>().FromSqlInterpolated(
                $"SELECT r.* FROM survey.survey_funding_receipts AS r WHERE r.survey_order_id = {order.Id} ORDER BY r.received_at, r.created_at, r.id FOR SHARE")
            : db.Set<SurveyFundingReceipt>().AsNoTracking()
                .Where(row => row.SurveyOrderId == order.Id)
                .OrderBy(row => row.ReceivedAt)
                .ThenBy(row => row.CreatedAt)
                .ThenBy(row => row.Id))
            .ToListAsync(cancellationToken);

        var allocations = await (useRelationalLock
            ? db.Set<SurveyFundingAllocation>().FromSqlInterpolated(
                $"SELECT a.* FROM survey.survey_funding_allocations AS a WHERE a.survey_order_id = {order.Id} ORDER BY a.id FOR SHARE")
            : db.Set<SurveyFundingAllocation>().AsNoTracking()
                .Where(row => row.SurveyOrderId == order.Id)
                .OrderBy(row => row.Id))
            .ToListAsync(cancellationToken);

        var reversals = await (useRelationalLock
            ? db.Set<SurveyFundingReversal>().FromSqlInterpolated(
                $"SELECT rev.* FROM survey.survey_funding_reversals AS rev WHERE rev.survey_order_id = {order.Id} ORDER BY rev.id FOR SHARE")
            : db.Set<SurveyFundingReversal>().AsNoTracking()
                .Where(row => row.SurveyOrderId == order.Id)
                .OrderBy(row => row.Id))
            .ToListAsync(cancellationToken);

        var refundObligations = await (useRelationalLock
            ? db.Set<SurveyRefundObligation>().FromSqlInterpolated(
                $"SELECT ro.* FROM survey.survey_refund_obligations AS ro WHERE ro.survey_order_id = {order.Id} ORDER BY ro.id FOR SHARE")
            : db.Set<SurveyRefundObligation>().AsNoTracking()
                .Where(row => row.SurveyOrderId == order.Id)
                .OrderBy(row => row.Id))
            .ToListAsync(cancellationToken);

        var reconciliationDecisions = await (useRelationalLock
            ? db.Set<SurveyFundingReconciliationDecision>().FromSqlInterpolated(
                $"SELECT d.* FROM survey.survey_funding_reconciliation_decisions AS d WHERE d.survey_order_id = {order.Id} ORDER BY d.reconciled_at, d.id FOR SHARE")
            : db.Set<SurveyFundingReconciliationDecision>().AsNoTracking()
                .Where(row => row.SurveyOrderId == order.Id)
                .OrderBy(row => row.ReconciledAt)
                .ThenBy(row => row.Id))
            .ToListAsync(cancellationToken);

        // Read existing invoice revision IDs for this exact order/tenant/farm to validate original invoice linkage
        // without acquiring Rank 6 locks inside Rank 5.
        var existingInvoiceIds = receipts.Any(r => r.InvoiceRevisionId.HasValue) ||
            reconciliationDecisions.Any(d => d.TargetInvoiceRevisionId.HasValue)
            ? await db.Set<SurveyInvoiceRevision>().AsNoTracking()
                .Where(inv => inv.SurveyOrderId == order.Id && inv.TenantId == order.TenantId && inv.FarmId == order.FarmId)
                .Select(inv => inv.Id)
                .ToHashSetAsync(cancellationToken)
            : new HashSet<Guid>();

        var allocatedByReceipt = allocations
            .GroupBy(a => a.FundingReceiptId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

        var receiptsById = receipts.ToDictionary(r => r.Id);
        var decisionsByReceipt = new Dictionary<Guid, SurveyFundingReconciliationDecision>();
        foreach (var decision in reconciliationDecisions)
        {
            if (decision.TenantId != order.TenantId ||
                decision.SurveyOrderId != order.Id ||
                !string.Equals(decision.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                decision.Amount < 0m ||
                decimal.Truncate(decision.Amount) != decision.Amount)
            {
                return Hold("Reconciliation decision currency, tenant, or amount integrity check failed.");
            }

            if (decision.SurveyPaymentId.HasValue && !orderPayments.ContainsKey(decision.SurveyPaymentId.Value))
            {
                return Hold("Reconciliation decision references a payment not belonging to this order.");
            }

            if (decision.TargetInvoiceRevisionId.HasValue && !existingInvoiceIds.Contains(decision.TargetInvoiceRevisionId.Value))
            {
                return Hold("Reconciliation decision references an unknown invoice revision.");
            }

            if (decision.FundingReceiptId.HasValue)
            {
                if (!receiptsById.TryGetValue(decision.FundingReceiptId.Value, out var targetReceipt))
                {
                    return Hold("Reconciliation decision references a funding receipt not belonging to this order.");
                }

                if (decision.Amount > targetReceipt.Amount)
                {
                    return Hold("Reconciliation decision amount exceeds the target funding receipt amount.");
                }

                // A financial disposition remains authoritative even when timestamps tie.
                if (decision.DispositionKind != SurveyReconciliationDispositionKind.KeepExcluded ||
                    !decisionsByReceipt.TryGetValue(targetReceipt.Id, out var previous) ||
                    previous.DispositionKind == SurveyReconciliationDispositionKind.KeepExcluded)
                {
                    decisionsByReceipt[targetReceipt.Id] = decision;
                }
            }
        }

        var receiptKinds = new Dictionary<Guid, SurveyFundingReceiptKind>(receipts.Count);
        var receiptEligibility = new Dictionary<Guid, bool>(receipts.Count);
        var receiptExclusionReasons = new Dictionary<Guid, string?>();
        var countedCashByReceipt = new Dictionary<Guid, decimal>(receipts.Count);
        var reconciledEligibleAmountByReceipt = new Dictionary<Guid, decimal>();
        var routedToRefundByReceipt = new Dictionary<Guid, decimal>();
        var reconciliationReasons = new List<string>();
        var excludedReceiptIds = new List<Guid>();

        var orderDepositPayments = orderPayments.Values
            .Where(p => p.Purpose == PaymentPurpose.OrderDeposit)
            .ToList();

        foreach (var receipt in receipts)
        {
            if (receipt.TenantId != order.TenantId ||
                !string.Equals(receipt.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                receipt.Amount <= 0m ||
                decimal.Truncate(receipt.Amount) != receipt.Amount)
            {
                return Hold("Funding receipt currency, tenant, or amount integrity check failed.");
            }

            if (receipt.DepositObligationId.HasValue)
            {
                if (receipt.InvoiceRevisionId.HasValue)
                {
                    return Hold("Deposit funding receipt cannot reference an invoice revision.");
                }

                if (!DepositObligationMatches(depositObligation, receipt.DepositObligationId.Value))
                {
                    return Hold("Funding receipt references a deposit obligation not pinned to this order.");
                }

                if (receipt.SurveyPaymentId.HasValue)
                {
                    if (!orderPayments.TryGetValue(receipt.SurveyPaymentId.Value, out var depositPayment) ||
                        depositPayment.SurveyOrderId != order.Id ||
                        depositPayment.Purpose != PaymentPurpose.OrderDeposit ||
                        depositPayment.DepositObligationId != receipt.DepositObligationId.Value ||
                        !string.Equals(depositPayment.Provider, receipt.Provider, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(depositPayment.ProviderReference, receipt.PaymentReference, StringComparison.Ordinal) ||
                        !string.Equals(depositPayment.Currency, receipt.Currency, StringComparison.OrdinalIgnoreCase))
                    {
                        return Hold("Deposit funding receipt payment linkage or reference check failed.");
                    }
                }
                else if (orderDepositPayments.Count > 0)
                {
                    var fallbackMatches = orderDepositPayments
                        .Where(p =>
                            p.DepositObligationId == receipt.DepositObligationId.Value &&
                            string.Equals(p.Provider, receipt.Provider, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(p.ProviderReference, receipt.PaymentReference, StringComparison.Ordinal) &&
                            string.Equals(p.Currency, receipt.Currency, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (fallbackMatches.Count != 1)
                    {
                        return Hold("Legacy deposit funding receipt could not be uniquely matched to an order deposit payment.");
                    }
                }

                receiptKinds[receipt.Id] = SurveyFundingReceiptKind.OrderDeposit;
                decisionsByReceipt.TryGetValue(receipt.Id, out var depositDecision);
                var isUnverifiedMissingTx = receipt.ProviderTransactionId.StartsWith("unverified-tx:", StringComparison.Ordinal);
                countedCashByReceipt[receipt.Id] = isUnverifiedMissingTx &&
                    (depositDecision is null ||
                     depositDecision.DispositionKind == SurveyReconciliationDispositionKind.KeepExcluded ||
                     string.IsNullOrWhiteSpace(depositDecision.ProviderTransactionId))
                    ? 0m
                    : receipt.Amount;

                if (depositDecision is { DispositionKind: SurveyReconciliationDispositionKind.AllocateToCurrentInvoice })
                {
                    return Hold("Deposit funding receipt cannot be reallocated as a balance receipt.");
                }
                else if (depositDecision is { DispositionKind: SurveyReconciliationDispositionKind.RouteToRefund })
                {
                    if (allocatedByReceipt.GetValueOrDefault(receipt.Id, 0m) > 0m)
                    {
                        return Hold("Allocated deposit funding receipt cannot be routed to refund directly via reconciliation.");
                    }

                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = "ReconciliationDecision.RouteToRefund";
                    excludedReceiptIds.Add(receipt.Id);
                    routedToRefundByReceipt[receipt.Id] = depositDecision.Amount;
                }
                else if (depositDecision is { DispositionKind: SurveyReconciliationDispositionKind.KeepExcluded })
                {
                    if (allocatedByReceipt.GetValueOrDefault(receipt.Id, 0m) > 0m)
                    {
                        return Hold("Allocated deposit funding receipt kept excluded cannot be isolated from settlement.");
                    }

                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = "ReconciliationDecision.KeepExcluded";
                    excludedReceiptIds.Add(receipt.Id);
                    AddReconciliationReason(reconciliationReasons, "ReconciliationDecision.KeepExcluded");
                }
                else if (receipt.RequiresReconciliation)
                {
                    // A RequiresReconciliation deposit receipt can only be excluded from settlement if it has no deposit allocations.
                    if (allocatedByReceipt.GetValueOrDefault(receipt.Id, 0m) > 0m)
                    {
                        return Hold("Allocated deposit funding receipt marked RequiresReconciliation cannot be isolated from settlement.");
                    }

                    var reason = receipt.ReconciliationReason ?? "DepositReceipt.RequiresReconciliation";
                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = reason;
                    excludedReceiptIds.Add(receipt.Id);
                    AddReconciliationReason(reconciliationReasons, reason);
                }
                else
                {
                    receiptEligibility[receipt.Id] = true;
                    receiptExclusionReasons[receipt.Id] = null;
                }
            }
            else
            {
                if (!receipt.SurveyPaymentId.HasValue ||
                    !orderPayments.TryGetValue(receipt.SurveyPaymentId.Value, out var balancePayment) ||
                    balancePayment.SurveyOrderId != order.Id ||
                    balancePayment.Purpose != PaymentPurpose.FinalBalance ||
                    !string.Equals(balancePayment.Provider, receipt.Provider, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(balancePayment.ProviderReference, receipt.PaymentReference, StringComparison.Ordinal) ||
                    !string.Equals(balancePayment.Currency, receipt.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    return Hold("Balance funding receipt payment linkage, scope, or reference check failed.");
                }

                if (balancePayment.Status == SurveyPaymentStatus.Invalidated)
                {
                    if (!receipt.RequiresReconciliation)
                    {
                        return Hold("Funding receipt on invalidated balance checkout must be flagged for reconciliation.");
                    }

                    if (receipt.InvoiceRevisionId.HasValue &&
                        (balancePayment.InvoiceRevisionId != receipt.InvoiceRevisionId.Value ||
                         !existingInvoiceIds.Contains(receipt.InvoiceRevisionId.Value)))
                    {
                        return Hold("Balance funding receipt on invalidated checkout references an unknown or mismatched invoice revision.");
                    }
                }
                else if (receipt.RequiresReconciliation)
                {
                    if (receipt.InvoiceRevisionId.HasValue &&
                        (!existingInvoiceIds.Contains(receipt.InvoiceRevisionId.Value) ||
                         (balancePayment.InvoiceRevisionId.HasValue && balancePayment.InvoiceRevisionId != receipt.InvoiceRevisionId.Value)))
                    {
                        return Hold("Held balance funding receipt has inconsistent invoice revision linkage.");
                    }
                }
                else if (!receipt.InvoiceRevisionId.HasValue ||
                         balancePayment.InvoiceRevisionId != receipt.InvoiceRevisionId.Value ||
                         !existingInvoiceIds.Contains(receipt.InvoiceRevisionId.Value))
                {
                    return Hold("Balance funding receipt lacks verified original invoice revision linkage.");
                }

                receiptKinds[receipt.Id] = SurveyFundingReceiptKind.FinalBalance;
                decisionsByReceipt.TryGetValue(receipt.Id, out var balanceDecision);
                var isUnverifiedMissingTx = receipt.ProviderTransactionId.StartsWith("unverified-tx:", StringComparison.Ordinal);
                countedCashByReceipt[receipt.Id] = isUnverifiedMissingTx &&
                    (balanceDecision is null ||
                     balanceDecision.DispositionKind == SurveyReconciliationDispositionKind.KeepExcluded ||
                     string.IsNullOrWhiteSpace(balanceDecision.ProviderTransactionId))
                    ? 0m
                    : receipt.Amount;

                if (balanceDecision is { DispositionKind: SurveyReconciliationDispositionKind.AllocateToCurrentInvoice })
                {
                    if (isUnverifiedMissingTx && string.IsNullOrWhiteSpace(balanceDecision.ProviderTransactionId))
                    {
                        return Hold("Reconciliation allocation for an unverified callback requires a verified ProviderTransactionId.");
                    }

                    if (!currentInvoiceRevisionId.HasValue ||
                        balanceDecision.TargetInvoiceRevisionId != currentInvoiceRevisionId.Value)
                    {
                        const string reason = "BalanceReceipt.ReconciledToNonCurrentInvoice";
                        receiptEligibility[receipt.Id] = false;
                        receiptExclusionReasons[receipt.Id] = reason;
                        excludedReceiptIds.Add(receipt.Id);
                        AddReconciliationReason(reconciliationReasons, reason);
                    }
                    else
                    {
                        receiptEligibility[receipt.Id] = true;
                        receiptExclusionReasons[receipt.Id] = null;
                        reconciledEligibleAmountByReceipt[receipt.Id] = balanceDecision.Amount;
                    }
                }
                else if (balanceDecision is { DispositionKind: SurveyReconciliationDispositionKind.RouteToRefund })
                {
                    if (isUnverifiedMissingTx && string.IsNullOrWhiteSpace(balanceDecision.ProviderTransactionId))
                    {
                        return Hold("Reconciliation refund routing for an unverified callback requires a verified ProviderTransactionId.");
                    }

                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = "ReconciliationDecision.RouteToRefund";
                    excludedReceiptIds.Add(receipt.Id);
                    routedToRefundByReceipt[receipt.Id] = balanceDecision.Amount;
                }
                else if (balanceDecision is { DispositionKind: SurveyReconciliationDispositionKind.KeepExcluded })
                {
                    const string reason = "ReconciliationDecision.KeepExcluded";
                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = reason;
                    excludedReceiptIds.Add(receipt.Id);
                    AddReconciliationReason(reconciliationReasons, reason);
                }
                else if (receipt.RequiresReconciliation)
                {
                    var reason = receipt.ReconciliationReason ??
                        (balancePayment.Status == SurveyPaymentStatus.Invalidated
                            ? "LATE_RECEIPT_ON_INVALIDATED_CHECKOUT"
                            : "BalanceReceipt.RequiresReconciliation");
                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = reason;
                    excludedReceiptIds.Add(receipt.Id);
                    AddReconciliationReason(reconciliationReasons, reason);
                }
                else if (!currentInvoiceRevisionId.HasValue ||
                         receipt.InvoiceRevisionId!.Value != currentInvoiceRevisionId.Value)
                {
                    const string reason = "BalanceReceipt.NonCurrentInvoiceRevision";
                    receiptEligibility[receipt.Id] = false;
                    receiptExclusionReasons[receipt.Id] = reason;
                    excludedReceiptIds.Add(receipt.Id);
                    AddReconciliationReason(reconciliationReasons, reason);
                }
                else
                {
                    receiptEligibility[receipt.Id] = true;
                    receiptExclusionReasons[receipt.Id] = null;
                }
            }
        }

        foreach (var allocation in allocations)
        {
            if (!DepositObligationMatches(depositObligation, allocation.DepositObligationId) ||
                !receiptsById.ContainsKey(allocation.FundingReceiptId) ||
                receiptKinds.GetValueOrDefault(allocation.FundingReceiptId) != SurveyFundingReceiptKind.OrderDeposit ||
                !string.Equals(allocation.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                allocation.AllocatedAmount <= 0m ||
                decimal.Truncate(allocation.AllocatedAmount) != allocation.AllocatedAmount)
            {
                return Hold("Funding allocation linkage, obligation, or currency could not be verified.");
            }
        }

        foreach (var reversal in reversals)
        {
            if (!DepositObligationMatches(depositObligation, reversal.DepositObligationId) ||
                !orderPayments.TryGetValue(reversal.SurveyPaymentId, out var payment) ||
                payment.SurveyOrderId != order.Id ||
                payment.Purpose != PaymentPurpose.OrderDeposit ||
                payment.DepositObligationId != reversal.DepositObligationId ||
                !reversal.FundingReceiptId.HasValue ||
                !receiptsById.TryGetValue(reversal.FundingReceiptId.Value, out var receipt) ||
                receiptKinds.GetValueOrDefault(receipt.Id) != SurveyFundingReceiptKind.OrderDeposit ||
                receipt.DepositObligationId != reversal.DepositObligationId ||
                (receipt.SurveyPaymentId.HasValue && receipt.SurveyPaymentId != payment.Id) ||
                !string.Equals(reversal.Provider, receipt.Provider, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(payment.Provider, receipt.Provider, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(payment.ProviderReference, receipt.PaymentReference, StringComparison.Ordinal) ||
                !string.Equals(reversal.Currency, receipt.Currency, StringComparison.OrdinalIgnoreCase) ||
                reversal.CashAmount is not > 0m ||
                decimal.Truncate(reversal.CashAmount.Value) != reversal.CashAmount.Value ||
                reversal.Amount < 0m ||
                reversal.Amount > reversal.CashAmount.Value ||
                decimal.Truncate(reversal.Amount) != reversal.Amount)
            {
                return Hold("A funding reversal requires verified original receipt and allocated cash linkage.");
            }
        }

        foreach (var receipt in receipts)
        {
            var allocatedOnReceipt = allocatedByReceipt.GetValueOrDefault(receipt.Id, 0m);
            if (allocatedOnReceipt > receipt.Amount)
            {
                return Hold($"Allocations on receipt '{receipt.Id}' exceed confirmed receipt amount.");
            }
        }

        var reversalsByReceipt = reversals
            .GroupBy(r => r.FundingReceiptId!.Value)
            .ToDictionary(g => g.Key, g => (Cash: g.Sum(r => r.CashAmount!.Value), Allocation: g.Sum(r => r.Amount)));
        foreach (var (receiptId, reversed) in reversalsByReceipt)
        {
            var receipt = receiptsById[receiptId];
            var allocation = allocatedByReceipt.GetValueOrDefault(receiptId);
            if (reversed.Cash > receipt.Amount ||
                reversed.Allocation != Math.Max(0m, reversed.Cash - (receipt.Amount - allocation)))
            {
                return Hold("Reversal totals disagree with original receipt allocations.");
            }
        }

        var allocatedGross = allocations.Sum(a => a.AllocatedAmount);
        var allocatedReversals = reversals.Sum(r => r.Amount);
        var allocatedNetAfterReversals = allocatedGross - allocatedReversals;

        if (allocatedNetAfterReversals < 0m)
        {
            return Hold("Funding reversals exceed allocated deposit amount.");
        }

        if (depositObligation is not null && allocatedNetAfterReversals != depositObligation.AllocatedAmount)
        {
            return Hold("Deposit obligation allocated projection disagrees with immutable allocation and reversal facts.");
        }

        decimal totalCompletedRefunds = 0m;
        decimal totalActiveReservations = 0m;
        decimal eligibleCompletedRefunds = 0m;
        decimal activeOverpaymentReservations = 0m;
        decimal activeOtherRefundReservations = 0m;
        var hasUnlinkedRefundObligations = false;
        var completedRefundsByReceipt = new Dictionary<Guid, decimal>();
        var activeRefundsByReceipt = new Dictionary<Guid, decimal>();
        var receiptsWithAnyObligationRow = new HashSet<Guid>();
        var activeOverpaymentObligations = new List<SurveyRefundObligation>();
        var activeOrCompletedRefundIds = new List<Guid>();

        foreach (var refund in refundObligations)
        {
            if (refund.TenantId != order.TenantId ||
                !string.Equals(refund.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                refund.Amount <= 0m ||
                decimal.Truncate(refund.Amount) != refund.Amount)
            {
                return Hold("Refund obligation currency, tenant, or amount integrity check failed.");
            }

            if (refund.FundingReceiptId.HasValue)
            {
                receiptsWithAnyObligationRow.Add(refund.FundingReceiptId.Value);
            }

            if (refund.Status == SurveyRefundObligationStatus.Cancelled)
                continue;

            if (refund.FundingReceiptId.HasValue)
            {
                if (!receiptsById.ContainsKey(refund.FundingReceiptId.Value))
                {
                    return Hold("Refund obligation references a funding receipt not belonging to this order.");
                }
            }
            else
            {
                hasUnlinkedRefundObligations = true;
                if (!allowHistoricalUnlinkedRefunds)
                {
                    return Hold("Active or completed refund obligation lacks verified receipt linkage for reconciliation.");
                }
            }

            activeOrCompletedRefundIds.Add(refund.Id);
            var countsTowardSettlement = !refund.FundingReceiptId.HasValue || receiptEligibility[refund.FundingReceiptId.Value];

            if (refund.Status == SurveyRefundObligationStatus.Completed)
            {
                totalCompletedRefunds += refund.Amount;
                if (countsTowardSettlement)
                {
                    eligibleCompletedRefunds += refund.Amount;
                }

                if (refund.FundingReceiptId.HasValue)
                {
                    completedRefundsByReceipt[refund.FundingReceiptId.Value] =
                        completedRefundsByReceipt.GetValueOrDefault(refund.FundingReceiptId.Value, 0m) + refund.Amount;
                }
            }
            else if (refund.Status is SurveyRefundObligationStatus.Pending or
                     SurveyRefundObligationStatus.Reserved or
                     SurveyRefundObligationStatus.Executing)
            {
                totalActiveReservations += refund.Amount;
                if (refund.OriginKind == SurveyRefundOriginKind.Overpayment)
                {
                    activeOverpaymentReservations += refund.Amount;
                    activeOverpaymentObligations.Add(refund);
                }
                else if (countsTowardSettlement)
                {
                    activeOtherRefundReservations += refund.Amount;
                }

                if (refund.FundingReceiptId.HasValue)
                {
                    activeRefundsByReceipt[refund.FundingReceiptId.Value] =
                        activeRefundsByReceipt.GetValueOrDefault(refund.FundingReceiptId.Value, 0m) + refund.Amount;
                }
            }
        }

        if (order.Scope == ContractScope.V3)
        {
            // An approved complaint reserves money. Recording the manual transfer converts
            // that reservation into an order-level cash refund using the same case row.
            var complaintCases = await (useRelationalLock
                ? db.Set<QualityRefundCase>().FromSqlInterpolated($"""
                    SELECT c.*, c.xmin FROM survey.quality_refund_cases c
                    WHERE c.survey_order_id = {order.Id} AND c.status IN ('Approved', 'Refunded')
                    ORDER BY c.id FOR SHARE
                    """)
                : db.Set<QualityRefundCase>().AsNoTracking().Where(c =>
                    c.SurveyOrderId == order.Id &&
                    (c.Status == QualityRefundStatus.Approved || c.Status == QualityRefundStatus.Refunded)))
                .ToListAsync(cancellationToken);

            foreach (var complaint in complaintCases)
            {
                if (complaint.TenantId != order.TenantId || complaint.FarmId != order.FarmId ||
                    complaint.ApprovedAmount is not decimal amount || amount <= 0m ||
                    decimal.Truncate(amount) != amount)
                {
                    return Hold("Complaint refund amount or order scope could not be verified.");
                }

                if (complaint.Status == QualityRefundStatus.Refunded)
                {
                    totalCompletedRefunds += amount;
                }
                else
                {
                    totalActiveReservations += amount;
                    activeOtherRefundReservations += amount;
                }
            }
        }

        var grossConfirmedReceipts = receipts.Sum(r => countedCashByReceipt[r.Id]);
        var totalCashReversals = reversalsByReceipt.Values.Sum(r => r.Cash);
        var routedToRefundTotal = routedToRefundByReceipt.Values.Sum();
        var orderConfirmedNetCash = grossConfirmedReceipts - totalCashReversals - totalCompletedRefunds;
        var availableRefundCap = orderConfirmedNetCash - totalActiveReservations - routedToRefundTotal;

        if (orderConfirmedNetCash < 0m || availableRefundCap < 0m)
        {
            return Hold("Existing reversals, completed refunds, routed refunds, and active refund reservations exceed confirmed receipts.");
        }

        var eligibleGrossReceipts = receipts
            .Where(r => receiptEligibility[r.Id])
            .Sum(r => reconciledEligibleAmountByReceipt.GetValueOrDefault(r.Id, r.Amount));
        var eligibleCashReversals = reversalsByReceipt
            .Where(kv => receiptEligibility[kv.Key])
            .Sum(kv => kv.Value.Cash);
        var eligibleConfirmedNetPaid = eligibleGrossReceipts - eligibleCashReversals - eligibleCompletedRefunds;
        if (eligibleConfirmedNetPaid < 0m)
        {
            return Hold("Eligible settlement reversals and completed refunds exceed eligible confirmed receipts.");
        }

        decimal allocatedDepositReductionFromRefunds = 0m;
        var receiptEntries = new List<SurveyReceiptFundingEntry>(receipts.Count);
        foreach (var receipt in receipts)
        {
            var allocatedOnReceipt = allocatedByReceipt.GetValueOrDefault(receipt.Id, 0m);
            var unallocatedOnReceipt = countedCashByReceipt[receipt.Id] - allocatedOnReceipt;
            var completed = completedRefundsByReceipt.GetValueOrDefault(receipt.Id, 0m);
            var active = activeRefundsByReceipt.GetValueOrDefault(receipt.Id, 0m);
            var routed = routedToRefundByReceipt.GetValueOrDefault(receipt.Id, 0m);
            var reversed = reversalsByReceipt.GetValueOrDefault(receipt.Id);
            var availableOnReceipt = countedCashByReceipt[receipt.Id] - reversed.Cash - completed - active - routed;

            if (availableOnReceipt < 0m || (reversed.Cash > 0m && (completed > 0m || routed > 0m)))
            {
                return Hold("Receipt refunds, routed refunds, and reversals exceed available funds or may describe the same transfer.");
            }

            allocatedDepositReductionFromRefunds += Math.Max(0m, completed - Math.Max(0m, unallocatedOnReceipt));

            receiptEntries.Add(new SurveyReceiptFundingEntry(
                Receipt: receipt,
                Kind: receiptKinds[receipt.Id],
                IsEligibleForCurrentInvoiceSettlement: receiptEligibility[receipt.Id],
                ExclusionReason: receiptExclusionReasons[receipt.Id],
                AllocatedDepositAmount: allocatedOnReceipt,
                UnallocatedAmount: Math.Max(0m, unallocatedOnReceipt),
                ReversedCashAmount: reversed.Cash,
                ReversedAllocationAmount: reversed.Allocation,
                CompletedRefundsAmount: completed,
                ActiveReservationsAmount: active,
                AvailableCashAmount: availableOnReceipt,
                HasAnyRefundObligationRow: receiptsWithAnyObligationRow.Contains(receipt.Id),
                RoutedToRefundAmount: routed,
                EligibleSettlementAmount: receiptEligibility[receipt.Id]
                    ? reconciledEligibleAmountByReceipt.GetValueOrDefault(receipt.Id, receipt.Amount)
                    : 0m));
        }

        var hasOpenAdjustmentPayment = orderPayments.Values.Any(p =>
            p.Purpose == PaymentPurpose.FinalBalance &&
            p.Status == SurveyPaymentStatus.AdjustmentRequired);
        if (hasOpenAdjustmentPayment)
        {
            AddReconciliationReason(reconciliationReasons, "BalancePayment.AdjustmentRequired");
        }

        var validAllocatedDepositNet = allocatedNetAfterReversals - allocatedDepositReductionFromRefunds;
        if (validAllocatedDepositNet < 0m ||
            validAllocatedDepositNet != decimal.Truncate(validAllocatedDepositNet) ||
            validAllocatedDepositNet > SurveyInvoiceRevision.MaxPersistableAmount ||
            eligibleConfirmedNetPaid > SurveyInvoiceRevision.MaxPersistableAmount ||
            orderConfirmedNetCash > SurveyInvoiceRevision.MaxPersistableAmount)
        {
            return Hold("Computed net allocated deposit or confirmed cash exceeds supported bounds.");
        }

        var hasOpenReconciliation = reconciliationReasons.Count > 0 || hasUnlinkedRefundObligations;
        return Result.Success(new SurveyInvoiceFundingSummary(
            ValidAllocatedDepositNet: validAllocatedDepositNet,
            ConfirmedNetReceipts: eligibleConfirmedNetPaid,
            EligibleGrossReceipts: eligibleGrossReceipts,
            EligibleCashReversals: eligibleCashReversals,
            EligibleCompletedRefunds: eligibleCompletedRefunds,
            GrossConfirmedReceipts: grossConfirmedReceipts,
            TotalCashReversals: totalCashReversals,
            TotalCompletedRefunds: totalCompletedRefunds,
            TotalActiveReservations: totalActiveReservations,
            ActiveOverpaymentReservations: activeOverpaymentReservations,
            ActiveOtherRefundReservations: activeOtherRefundReservations,
            OrderConfirmedNetCash: orderConfirmedNetCash,
            AvailableRefundCap: availableRefundCap,
            HasUnlinkedRefundObligations: hasUnlinkedRefundObligations,
            HasOpenReconciliation: hasOpenReconciliation,
            ReconciliationReasons: reconciliationReasons,
            ExcludedReceiptIds: excludedReceiptIds,
            Receipts: receiptEntries,
            ReceiptIds: receipts.Select(r => r.Id).ToList(),
            ReversalIds: reversals.Select(r => r.Id).ToList(),
            ActiveOrCompletedRefundIds: activeOrCompletedRefundIds,
            ActiveOverpaymentObligations: activeOverpaymentObligations,
            RoutedToRefundAmount: routedToRefundTotal,
            ReconciliationDecisionIds: reconciliationDecisions.Select(d => d.Id).ToList()));

        static Result<SurveyInvoiceFundingSummary> Hold(string description) =>
            Result.Failure<SurveyInvoiceFundingSummary>(AppError.Conflict(HoldErrorCode, description));

        static bool DepositObligationMatches(SurveyDepositObligation? obligation, Guid candidateObligationId) =>
            obligation is not null && obligation.Id == candidateObligationId;

        static void AddReconciliationReason(List<string> reasons, string reason)
        {
            if (!reasons.Contains(reason, StringComparer.Ordinal))
                reasons.Add(reason);
        }
    }
}
