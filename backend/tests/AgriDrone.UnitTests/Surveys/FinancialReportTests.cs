using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Features.Reports;
using AgriDrone.Modules.Surveys.Infrastructure.Queries;
using Xunit;

namespace AgriDrone.UnitTests.Surveys;

public sealed class FinancialReportTests
{
    private static readonly Guid OrderId = Guid.Parse("6632dd80-937b-4bee-8a23-a6f26b95866f");
    private static readonly DateTimeOffset Coverage = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void MonthUsesVietnamBoundaryAndExclusiveEnd()
    {
        var period = FinancialReportPeriod.Create(2026, 11);
        Assert.Equal(17, period.StartUtc.Hour);
        Assert.Equal(31, period.StartUtc.Day);
        Assert.True(period.Contains(period.StartUtc));
        Assert.False(period.Contains(period.EndUtc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void InvalidMonthIsRejected(int month) => Assert.Throws<ArgumentOutOfRangeException>(() => FinancialReportPeriod.Create(2026, month));

    [Fact]
    public void ExcessReturnDoesNotReduceServiceRevenue()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "original_service_publication", new { amount = 60000, published_at = "2026-11-04T00:00:00Z" }));
        rows.Add(Row(4, "survey_funding_receipts", new { amount = 100000, received_at = "2026-11-02T00:00:00Z" }));
        rows.Add(Row(5, "survey_refund_obligations", new { amount = 40000, completed_at = "2026-11-05T00:00:00Z", origin_kind = "OVERPAYMENT" }));
        var line = Calculate(rows);
        Assert.Equal(60000m, line.PublishedServiceValue - line.ServiceRefunds);
        Assert.Equal(20000m, line.NetRevenue);
        Assert.Equal(40000m, line.OverpaymentRefunds);
        Assert.Equal(60000m, line.CashReceipts - line.CashRefunds);
        Assert.Equal(0m, line.RefundableExcess);
    }

    [Fact]
    public void ComplaintRefundFromEarlierSaleCanMakeRevenueNegative()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "original_service_publication", new { amount = 60000, published_at = "2026-10-04T00:00:00Z" }));
        rows.Add(Row(4, "survey_refund_obligations", new { amount = 20000, completed_at = "2026-11-05T00:00:00Z", origin_kind = "COMPLAINT_REFUND" }));
        var line = Calculate(rows);
        Assert.Equal(-20000m, line.PublishedServiceValue - line.ServiceRefunds);
        Assert.Equal(0m, line.OutstandingBalance);
    }

    [Fact]
    public void CorrectedInvoiceDoesNotRewriteOriginalSale()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "original_service_publication", new { amount = 60000, published_at = "2026-11-04T00:00:00Z" }));
        rows.Add(Row(4, "survey_orders", new { order_number = "ORD-1", final_price = 80000, status = "COMPLETED", created_at = "2026-10-01T00:00:00Z" }));
        Assert.Equal(60000m, Calculate(rows).PublishedServiceValue);
    }

    [Fact]
    public void PublishedLegacyComboUsesOriginalInvoiceAndLockedService()
    {
        var rows = BaseRows();
        rows.RemoveAt(1);
        rows.Add(Row(3, "survey_orders", new { order_number = "ORD-1", booked_service_code = "COMBO",
            final_price = 80000, status = "COMPLETED", created_at = "2026-10-01T00:00:00Z" }));
        rows.Add(Row(4, "legacy_service_publication", new { amount = 60000, published_at = "2026-11-04T00:00:00Z" }));
        var line = Calculate(rows);

        Assert.Equal("COMBO", line.Service);
        Assert.True(line.PublishedInPeriod);
        Assert.Equal(60000m, line.PublishedServiceValue);
        Assert.Equal(0m, line.OutstandingBalance);
    }

    [Fact]
    public void UnpricedDepositIsHeldWithoutInventingReceivable()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_orders", new { order_number = "ORD-1", status = "AWAITING_PRICING", created_at = "2026-10-01T00:00:00Z" }));
        rows.Add(Row(4, "survey_funding_receipts", new { amount = 10000, deposit_obligation_id = OrderId, received_at = "2026-11-02T00:00:00Z" }));
        var line = Calculate(rows);
        Assert.Equal(10000m, line.DepositsHeld);
        Assert.Equal(0m, line.OutstandingBalance);
        Assert.True(line.Unpriced);
    }

    [Fact]
    public void PendingRefundDoesNotChangeCash()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_refund_obligations", new { amount = 10000, status = "RESERVED", origin_kind = "OVERPAYMENT" }));
        var line = Calculate(rows);
        Assert.Equal(0m, line.CashRefunds);
        Assert.Equal(10000m, line.RefundPayable);
    }

    [Fact]
    public void OrderDepositPaymentAndReceiptAreNotCountedTwice()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_funding_receipts", new { amount = 10000, received_at = "2026-11-02T00:00:00Z" }));
        rows.Add(Row(4, "survey_payments", new { amount = 10000, confirmed_at = "2026-11-02T00:00:00Z", purpose = "OrderDeposit" }));
        Assert.Equal(10000m, Calculate(rows).CashReceipts);
    }

    [Fact]
    public void CancellationRetentionIsSeparateFromSalesAndHeldDeposits()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_orders", new { order_number = "ORD-1", final_price = 60000, status = "CANCELLED", created_at = "2026-10-01T00:00:00Z" }));
        rows.Add(Row(4, "survey_funding_receipts", new { amount = 10000, received_at = "2026-11-02T00:00:00Z" }));
        rows.Add(Row(5, "cancellation_retentions", new { amount = 10000, recorded_at = "2026-11-03T00:00:00Z" }));
        var line = Calculate(rows);
        Assert.Equal(10000m, line.CancellationRetention);
        Assert.Equal(0m, line.PublishedServiceValue);
        Assert.Equal(0m, line.DepositsHeld);
        Assert.Equal(0m, line.OutstandingBalance);
    }

    [Fact]
    public void PriorMonthDepositDoesNotBecomeThisMonthsCashReceipt()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_funding_receipts", new { amount = 10000, deposit_obligation_id = OrderId, received_at = "2026-10-02T00:00:00Z" }));
        var line = Calculate(rows);
        Assert.Equal(0m, line.CashReceipts);
        Assert.Equal(10000m, line.DepositsHeld);
        Assert.Equal(50000m, line.OutstandingBalance);
    }

    [Fact]
    public void FinalBalanceUsesActualReceiptInsteadOfCheckoutAmount()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_funding_receipts", new { amount = 70000, received_at = "2026-11-02T00:00:00Z" }));
        rows.Add(Row(4, "survey_payments", new { amount = 60000, confirmed_at = "2026-11-02T00:00:00Z", purpose = "FinalBalance" }));
        Assert.Equal(70000m, Calculate(rows).CashReceipts);
    }

    [Fact]
    public void ReceivedBalanceDoesNotIncreaseDepositsHeldBeforePublication()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_payments", new { amount = 10000, confirmed_at = "2026-11-02T00:00:00Z", purpose = "BaselineDeposit" }));
        rows.Add(Row(4, "survey_funding_receipts", new { amount = 50000, received_at = "2026-11-03T00:00:00Z" }));
        var line = Calculate(rows);

        Assert.Equal(60000m, line.CashReceipts);
        Assert.Equal(10000m, line.DepositsHeld);
        Assert.Equal(0m, line.OutstandingBalance);
    }

    [Theory]
    [InlineData(true, 15000, 25000, false)]
    [InlineData(false, 0, 40000, true)]
    public void DepositReversalCashAndCompletedRefundsAreReportedSeparately(
        bool receiptIsLinked,
        int expectedReversalCash,
        int expectedDepositsHeld,
        bool needsReconciliation)
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_funding_receipts", new
        {
            amount = 40000,
            deposit_obligation_id = OrderId,
            received_at = "2026-11-02T00:00:00Z"
        }));
        rows.Add(Row(4, "survey_payments", new
        {
            amount = 10000,
            confirmed_at = "2026-11-03T00:00:00Z",
            purpose = "FinalBalance",
            provider = "test",
            provider_reference = "balance"
        }));
        rows.Add(Row(5, "survey_funding_reversals", new
        {
            amount = receiptIsLinked ? 3000 : 0,
            cash_amount = 15000,
            funding_receipt_id = receiptIsLinked ? (Guid?)OrderId : null,
            occurred_at = "2026-11-05T00:00:00Z"
        }));
        rows.Add(Row(6, "survey_refund_obligations", new
        {
            amount = 5000,
            completed_at = "2026-11-06T00:00:00Z",
            origin_kind = "OVERPAYMENT"
        }));

        var line = Calculate(rows);

        Assert.Equal(5000m, line.OverpaymentRefunds);
        Assert.Equal(5000m + expectedReversalCash, line.CashRefunds);
        Assert.Equal((decimal)expectedReversalCash, line.FundingReversals);
        Assert.Equal((decimal)expectedDepositsHeld, line.DepositsHeld);
        Assert.Equal(needsReconciliation, line.NeedsReconciliation);
    }

    [Fact]
    public void OldCompletedOrderWithoutOriginalPublicationNeedsReconciliation()
    {
        var rows = BaseRows();
        rows.Add(Row(3, "survey_orders", new { order_number = "ORD-1", final_price = 60000, status = "COMPLETED", created_at = "2025-10-01T00:00:00Z" }));
        var line = Calculate(rows);
        Assert.True(line.NeedsReconciliation);
        Assert.Equal(0m, line.PublishedServiceValue);
        Assert.Equal(0m, line.OutstandingBalance);
    }

    private static FinancialReportLine Calculate(List<FinancialHistoryRow> rows) =>
        FinancialReportCalculator.Calculate(rows.ToArray(), FinancialReportPeriod.Create(2026, 11), Coverage)!;
    private static List<FinancialHistoryRow> BaseRows() =>
    [
        Row(1, "survey_orders", new { order_number = "ORD-1", final_price = 60000, status = "AWAITING_SETTLEMENT", created_at = "2026-10-01T00:00:00Z" }),
        Row(2, "order_commercial_snapshots", new { service_code = "MAP" })
    ];
    private static FinancialHistoryRow Row(long sequence, string source, object data) =>
        new(sequence, source, OrderId, OrderId, Coverage.AddMonths(10), JsonSerializer.SerializeToElement(data), false);
}
