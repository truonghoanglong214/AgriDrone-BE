using AgriDrone.Modules.Surveys.Application.Features.Reports;

namespace AgriDrone.Modules.Surveys.Infrastructure.Queries;

internal static class FinancialReportCalculator
{
    public static FinancialReportLine? Calculate(FinancialHistoryRow[] history, FinancialReportPeriod period, DateTimeOffset coverage)
    {
        var latest = history.GroupBy(x => (x.Source, x.SourceId)).Select(x => x.MaxBy(y => y.Sequence)!)
            .Where(x => !x.Deleted).ToArray();
        var order = latest.SingleOrDefault(x => x.Source == "survey_orders");
        if (order is null) return null;
        var snapshot = latest.SingleOrDefault(x => x.Source == "order_commercial_snapshots");
        var service = snapshot?.Text("service_code") ?? order.Text("booked_service_code") ?? "UNKNOWN";
        if (service is not ("MAP" or "COMBO")) service = "UNKNOWN";
        // The migration records the first actual publication atomically with the source transaction.
        var recordedSale = history.Where(x => x.Source == "original_service_publication").MinBy(x => x.Sequence);
        var sale = recordedSale ?? history.Where(x => x.Source == "legacy_service_publication")
            .MinBy(x => x.Time("published_at"));
        var saleAt = sale?.Time("published_at");
        var invoice = sale?.Money("amount") ?? 0;
        var total = order.Money("final_price");
        var unpriced = order.Text("final_price") is null;
        var cancelled = string.Equals(order.Text("status"), "CANCELLED", StringComparison.OrdinalIgnoreCase);
        var released = recordedSale is not null || string.Equals(order.Text("status"), "COMPLETED", StringComparison.OrdinalIgnoreCase);
        var reconciles = (released && sale is null) || (sale is null && history.Any(x => x.Source == "survey_results" && x.Time("published_at") is not null)) || service == "UNKNOWN" || order.Time("created_at") < coverage ||
            (sale is not null && (invoice <= 0 || saleAt is null));
        var receipts = latest.Where(x => x.Source == "survey_funding_receipts").ToArray();
        var payments = latest.Where(x => x.Source == "survey_payments" && x.Time("confirmed_at") is not null &&
            x.Text("purpose") != "OrderDeposit" &&
            !(x.Text("purpose") == "FinalBalance" && receipts.Any(r => r.Text("deposit_obligation_id") is null)) &&
            !receipts.Any(r => r.Text("provider") == x.Text("provider") &&
                r.Text("provider_transaction_id") == x.Text("provider_reference"))).ToArray();
        var pendingPayments = latest.Where(x => x.Source == "survey_payments" &&
            x.Text("status") is "PENDING" or "PROCESSING").Sum(x => x.Money("amount"));
        var received = receipts.Sum(x => x.Money("amount")) + payments.Sum(x => x.Money("amount"));
        var cashIn = receipts.Where(x => period.Contains(x.Time("received_at"))).Sum(x => x.Money("amount")) +
            payments.Where(x => period.Contains(x.Time("confirmed_at"))).Sum(x => x.Money("amount"));
        var refunds = latest.Where(x => x.Source == "survey_refund_obligations").ToArray();
        var completed = refunds.Where(x => x.Time("completed_at") is not null).ToArray();
        var quality = latest.Where(x => x.Source == "quality_refund_cases" && x.Time("transferred_at") is not null &&
            !completed.Any(r => r.Text("origin_decision_id") == x.SourceId.ToString())).ToArray();
        var reversals = latest.Where(x => x.Source == "survey_funding_reversals").ToArray();
        reconciles |= receipts.Any(r => string.Equals(r.Text("requires_reconciliation"), "true", StringComparison.OrdinalIgnoreCase)) ||
            reversals.Any(r => r.Text("cash_amount") is not null && r.Text("funding_receipt_id") is null);
        var completedInPeriod = completed.Where(x => period.Contains(x.Time("completed_at"))).ToArray();
        var overpayment = completedInPeriod.Where(x => x.Text("origin_kind") is "OVERPAYMENT" or "Overpayment").Sum(x => x.Money("amount"));
        var cancellation = completedInPeriod.Where(x => x.Text("origin_kind") is "CANCELLATION_RETURN" or "CancellationReturn").Sum(x => x.Money("amount"));
        var serviceFailure = completedInPeriod.Where(x => x.Text("origin_kind") is "SERVICE_FAILURE" or "ServiceFailure").Sum(x => x.Money("amount"));
        var complaint = completedInPeriod.Where(x => x.Text("origin_kind") is "COMPLAINT_REFUND" or "ComplaintRefund").Sum(x => x.Money("amount"));
        var legacyComplaint = quality.Where(x => period.Contains(x.Time("transferred_at"))).Sum(x => x.Money("approved_amount"));
        var completedBankRefunds = completedInPeriod.Sum(x => x.Money("amount"));
        var unclassified = completedBankRefunds - overpayment - cancellation - serviceFailure - complaint;
        var reversedDepositCash = reversals.Sum(ReconciledReversalCash);
        var refunded = completed.Sum(x => x.Money("amount")) + quality.Sum(x => x.Money("approved_amount")) + reversedDepositCash;
        var cashOut = completedBankRefunds + legacyComplaint +
            reversals.Where(x => period.Contains(x.Time("occurred_at"))).Sum(ReconciledReversalCash);
        var serviceRefunds = complaint + legacyComplaint;
        reconciles |= unclassified != 0;
        // A service-value refund requires a known original publication, otherwise reconcile it separately.
        if (sale is null && serviceRefunds > 0) { reconciles = true; serviceRefunds = 0; }
        var retentions = latest.Where(x => x.Source == "cancellation_retentions").ToArray();
        var retained = retentions.Sum(x => x.Money("amount"));
        var netFunding = Math.Max(0, received - refunded - retained);
        var excess = unpriced ? 0 : Math.Max(0, netFunding - total);
        var receivedDeposits = receipts.Where(x => x.Text("deposit_obligation_id") is not null).Sum(x => x.Money("amount")) +
            payments.Where(x => x.Text("purpose") == "BaselineDeposit").Sum(x => x.Money("amount"));
        var remainingDeposits = Math.Max(0, receivedDeposits - reversedDepositCash - retained);
        var held = !released && !cancelled ? Math.Min(remainingDeposits, netFunding - excess) : 0;
        var payable = refunds.Where(x => x.Time("completed_at") is null &&
            !string.Equals(x.Text("status"), "CANCELLED", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Money("amount")) + latest.Where(x => x.Source == "quality_refund_cases" &&
                x.Text("status") == "Approved" && !refunds.Any(r => r.Text("origin_decision_id") == x.SourceId.ToString()))
                .Sum(x => x.Money("approved_amount"));
        // Complaint refunds do not manufacture new arrears against an already released invoice.
        var outstanding = !cancelled && !released && !unpriced ? Math.Max(0, total - netFunding) : 0;
        return new(order.OrderId, order.Text("order_number") ?? order.OrderId.ToString(), service,
            period.Contains(saleAt) ? invoice : 0, serviceRefunds, cashIn, cashOut, held, excess, payable,
            retentions.Where(x => period.Contains(x.Time("recorded_at"))).Sum(x => x.Money("amount")),
            outstanding, pendingPayments, period.Contains(saleAt), !cancelled && !released && unpriced, reconciles)
        {
            OverpaymentRefunds = overpayment,
            CancellationRefunds = cancellation,
            ServiceFailureRefunds = serviceFailure,
            ComplaintRefunds = complaint + legacyComplaint,
            UnclassifiedRefunds = unclassified
        };
    }

    private static decimal ReconciledReversalCash(FinancialHistoryRow reversal) =>
        reversal.Text("cash_amount") is null
            ? reversal.Money("amount")
            : reversal.Text("funding_receipt_id") is not null ? reversal.Money("cash_amount") : 0;
}
