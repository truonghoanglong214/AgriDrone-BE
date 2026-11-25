using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Surveys;

internal sealed record SurveyInvoiceFundingTotals(
    decimal ValidAllocatedDepositNet,
    decimal ConfirmedNetReceipts);

internal static class SurveyInvoiceFunding
{
    // R05E-02: Compute ValidAllocatedDepositNet from actual allocations to pinned DepositObligation,
    // applying reversals/refunds strictly along verified linkage and failing closed on ambiguous facts.
    internal static async Task<Result<SurveyInvoiceFundingTotals>> ReadAsync(
        AgriDroneSchemaDbContext db,
        SurveyOrder order,
        SurveyDepositObligation? depositObligation,
        Dictionary<Guid, SurveyPayment> depositPayments,
        bool lockRows,
        CancellationToken cancellationToken)
    {
        var receipts = await (lockRows
            ? db.Set<SurveyFundingReceipt>().FromSqlInterpolated(
                $"SELECT * FROM survey.survey_funding_receipts WHERE survey_order_id = {order.Id} FOR SHARE")
            : db.Set<SurveyFundingReceipt>().AsNoTracking().Where(row => row.SurveyOrderId == order.Id))
            .ToListAsync(cancellationToken);

        var allocations = await (lockRows
            ? db.Set<SurveyFundingAllocation>().FromSqlInterpolated(
                $"SELECT * FROM survey.survey_funding_allocations WHERE survey_order_id = {order.Id} FOR SHARE")
            : db.Set<SurveyFundingAllocation>().AsNoTracking().Where(row => row.SurveyOrderId == order.Id))
            .ToListAsync(cancellationToken);

        var reversals = await (lockRows
            ? db.Set<SurveyFundingReversal>().FromSqlInterpolated(
                $"SELECT * FROM survey.survey_funding_reversals WHERE survey_order_id = {order.Id} FOR SHARE")
            : db.Set<SurveyFundingReversal>().AsNoTracking().Where(row => row.SurveyOrderId == order.Id))
            .ToListAsync(cancellationToken);

        var refundObligations = await (lockRows
            ? db.Set<SurveyRefundObligation>().FromSqlInterpolated(
                $"SELECT * FROM survey.survey_refund_obligations WHERE survey_order_id = {order.Id} FOR SHARE")
            : db.Set<SurveyRefundObligation>().AsNoTracking().Where(row => row.SurveyOrderId == order.Id))
            .ToListAsync(cancellationToken);

        var receiptsById = receipts.ToDictionary(r => r.Id);

        foreach (var receipt in receipts)
        {
            if (receipt.TenantId != order.TenantId ||
                !string.Equals(receipt.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                receipt.Amount <= 0m ||
                decimal.Truncate(receipt.Amount) != receipt.Amount)
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "Funding receipt currency, tenant, or amount integrity check failed."));
            }

            if (receipt.DepositObligationId.HasValue &&
                (!depositObligationOrderMatches(depositObligation, receipt.DepositObligationId.Value)))
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "Funding receipt references a deposit obligation not pinned to this order."));
            }
        }

        foreach (var allocation in allocations)
        {
            if (!depositObligationOrderMatches(depositObligation, allocation.DepositObligationId) ||
                !receiptsById.ContainsKey(allocation.FundingReceiptId) ||
                !string.Equals(allocation.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                allocation.AllocatedAmount <= 0m ||
                decimal.Truncate(allocation.AllocatedAmount) != allocation.AllocatedAmount)
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "Funding allocation linkage, obligation, or currency could not be verified."));
            }
        }

        foreach (var reversal in reversals)
        {
            if (!depositObligationOrderMatches(depositObligation, reversal.DepositObligationId) ||
                !depositPayments.TryGetValue(reversal.SurveyPaymentId, out var payment) ||
                !reversal.FundingReceiptId.HasValue ||
                !receiptsById.TryGetValue(reversal.FundingReceiptId.Value, out var receipt) ||
                receipt.DepositObligationId != reversal.DepositObligationId ||
                (receipt.SurveyPaymentId.HasValue && receipt.SurveyPaymentId != payment.Id) ||
                !string.Equals(reversal.Provider, receipt.Provider, StringComparison.Ordinal) ||
                !string.Equals(payment.Provider, receipt.Provider, StringComparison.Ordinal) ||
                !string.Equals(payment.ProviderReference, receipt.PaymentReference, StringComparison.Ordinal) ||
                !string.Equals(reversal.Currency, receipt.Currency, StringComparison.Ordinal) ||
                reversal.CashAmount is not > 0m ||
                reversal.Amount < 0m || reversal.Amount > reversal.CashAmount.Value)
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "A funding reversal requires verified original receipt and allocated cash linkage."));
            }
        }

        var allocatedByReceipt = allocations
            .GroupBy(a => a.FundingReceiptId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

        foreach (var receipt in receipts)
        {
            var allocatedOnReceipt = allocatedByReceipt.GetValueOrDefault(receipt.Id, 0m);
            if (allocatedOnReceipt > receipt.Amount)
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    $"Allocations on receipt '{receipt.Id}' exceed confirmed receipt amount."));
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
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold", "Reversal totals disagree with original receipt allocations."));
            }
        }

        var allocatedGross = allocations.Sum(a => a.AllocatedAmount);
        var allocatedReversals = reversals.Sum(r => r.Amount);
        var allocatedNetAfterReversals = allocatedGross - allocatedReversals;

        if (allocatedNetAfterReversals < 0m)
        {
            return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                "InvoiceFinalization.RefundReconciliationHold",
                "Funding reversals exceed allocated deposit amount."));
        }

        if (depositObligation is not null && allocatedNetAfterReversals != depositObligation.AllocatedAmount)
        {
            return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                "InvoiceFinalization.RefundReconciliationHold",
                "Deposit obligation allocated projection disagrees with immutable allocation and reversal facts."));
        }

        decimal totalCompletedRefunds = 0m;
        decimal totalActiveReservations = 0m;
        var completedRefundsByReceipt = new Dictionary<Guid, decimal>();
        var activeRefundsByReceipt = new Dictionary<Guid, decimal>();

        foreach (var refund in refundObligations)
        {
            if (refund.TenantId != order.TenantId ||
                !string.Equals(refund.Currency, order.Currency, StringComparison.OrdinalIgnoreCase) ||
                refund.Amount <= 0m ||
                decimal.Truncate(refund.Amount) != refund.Amount)
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "Refund obligation currency, tenant, or amount integrity check failed."));
            }

            if (refund.Status == SurveyRefundObligationStatus.Cancelled)
                continue;

            // Facts without receipt linkage have indeterminate impact on allocated deposit vs unallocated excess -> fail closed
            if (!refund.FundingReceiptId.HasValue || !receiptsById.TryGetValue(refund.FundingReceiptId.Value, out _))
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "Active or completed refund obligation lacks verified receipt linkage for reconciliation."));
            }

            if (refund.Status == SurveyRefundObligationStatus.Completed)
            {
                totalCompletedRefunds += refund.Amount;
                completedRefundsByReceipt[refund.FundingReceiptId.Value] =
                    completedRefundsByReceipt.GetValueOrDefault(refund.FundingReceiptId.Value, 0m) + refund.Amount;
            }
            else if (refund.Status is SurveyRefundObligationStatus.Pending or
                     SurveyRefundObligationStatus.Reserved or
                     SurveyRefundObligationStatus.Executing)
            {
                totalActiveReservations += refund.Amount;
                activeRefundsByReceipt[refund.FundingReceiptId.Value] =
                    activeRefundsByReceipt.GetValueOrDefault(refund.FundingReceiptId.Value) + refund.Amount;
            }
        }

        var totalConfirmedReceipts = receipts.Sum(r => r.Amount);
        var confirmedNetReceipts = totalConfirmedReceipts - reversalsByReceipt.Values.Sum(r => r.Cash) - totalCompletedRefunds;

        if (confirmedNetReceipts < 0m || confirmedNetReceipts - totalActiveReservations < 0m)
        {
            return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                "InvoiceFinalization.RefundReconciliationHold",
                "Existing reversals, completed refunds, and active refund reservations exceed confirmed receipts."));
        }

        decimal allocatedDepositReductionFromRefunds = 0m;
        foreach (var receipt in receipts)
        {
            var completed = completedRefundsByReceipt.GetValueOrDefault(receipt.Id);
            var active = activeRefundsByReceipt.GetValueOrDefault(receipt.Id);
            var reversed = reversalsByReceipt.GetValueOrDefault(receipt.Id);
            if (reversed.Cash + completed + active > receipt.Amount ||
                (reversed.Cash > 0m && completed > 0m))
            {
                return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                    "InvoiceFinalization.RefundReconciliationHold",
                    "Receipt refunds and reversals exceed available funds or may describe the same transfer."));
            }

            var unallocated = receipt.Amount - allocatedByReceipt.GetValueOrDefault(receipt.Id);
            allocatedDepositReductionFromRefunds += Math.Max(0m, completed - unallocated);
        }

        var validAllocatedDepositNet = allocatedNetAfterReversals - allocatedDepositReductionFromRefunds;
        if (validAllocatedDepositNet < 0m ||
            validAllocatedDepositNet != decimal.Truncate(validAllocatedDepositNet) ||
            validAllocatedDepositNet > SurveyInvoiceRevision.MaxPersistableAmount ||
            confirmedNetReceipts > SurveyInvoiceRevision.MaxPersistableAmount)
        {
            return Result.Failure<SurveyInvoiceFundingTotals>(AppError.Conflict(
                "InvoiceFinalization.RefundReconciliationHold",
                "Computed net allocated deposit or confirmed cash exceeds supported bounds."));
        }

        return Result.Success(new SurveyInvoiceFundingTotals(validAllocatedDepositNet, confirmedNetReceipts));

        static bool depositObligationOrderMatches(SurveyDepositObligation? obligation, Guid candidateObligationId) =>
            obligation is not null && obligation.Id == candidateObligationId;
    }
}
