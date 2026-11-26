using System.Globalization;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Features.Reports;
using AgriDrone.Modules.Surveys.Infrastructure.Queries;
using Xunit;

namespace AgriDrone.UnitTests.Surveys;

/// <summary>
/// TC-NFR-07 &amp; TC-E2E-05 calculator verification:
/// Seeds MAP/COMBO orders and transactions on both sides of 00:00 1st of month UTC+7
/// (17:00:00Z previous UTC day), deposits across months, remaining balance, completed complaint refund,
/// pending payments excluded, and corrected invoice not double-counted.
/// </summary>
public sealed class FinancialReconciliationNfr07Tests
{
    private static readonly DateTimeOffset Coverage =
        DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture);

    [Fact, Trait("TC", "TC-NFR-07"), Trait("TC", "TC-E2E-05")]
    public void UtcPlus7MonthBoundariesDepositBalanceRefundAndPendingReconcileAcrossThreePeriods()
    {
        var orderId = Guid.Parse("7732dd80-937b-4bee-8a23-a6f26b95866f");

        // Order MAP: 150 poles * 1,000 VND = 150,000 VND.
        // Deposit 100,000 VND at 2026-10-31 23:59:59 +07 (16:59:59Z) -> belongs to October.
        // Balance 50,000 VND + Publication at 2026-11-01 00:00:00 +07 (17:00:00Z) -> belongs to November.
        // Pending checkout 50,000 VND -> must be excluded from cash receipts and tracked as PendingPayments.
        // Corrected invoice changes final_price to 160,000 -> original_service_publication (150,000) stays authoritative.
        // Complaint refund 20,000 VND completed in December -> belongs to December.
        var rows = new List<FinancialHistoryRow>
        {
            Row(1, orderId, "survey_orders", new
            {
                order_number = "ORD-E2E-05",
                final_price = 160_000m,
                status = "COMPLETED",
                created_at = "2026-10-15T02:00:00Z"
            }),
            Row(2, orderId, "order_commercial_snapshots", new
            {
                service_code = "MAP"
            }),
            Row(3, orderId, "survey_funding_receipts", new
            {
                amount = 100_000m,
                received_at = "2026-10-31T16:59:59Z", // 23:59:59 UTC+7 Oct 31
                deposit_obligation_id = Guid.NewGuid(),
                provider = "SEPAY",
                provider_transaction_id = "TX-OCT-DEP"
            }),
            Row(4, orderId, "survey_funding_receipts", new
            {
                amount = 50_000m,
                received_at = "2026-10-31T17:00:00Z", // 00:00:00 UTC+7 Nov 1
                provider = "SEPAY",
                provider_transaction_id = "TX-NOV-BAL"
            }),
            Row(5, orderId, "survey_payments", new
            {
                amount = 50_000m,
                status = "PROCESSING",
                purpose = "FinalBalance"
            }),
            Row(6, orderId, "original_service_publication", new
            {
                amount = 150_000m,
                published_at = "2026-10-31T17:00:00Z" // 00:00:00 UTC+7 Nov 1
            }),
            Row(7, orderId, "survey_refund_obligations", new
            {
                amount = 20_000m,
                completed_at = "2026-12-05T03:00:00Z",
                origin_kind = "COMPLAINT_REFUND"
            })
        };

        var octLine = FinancialReportCalculator.Calculate([.. rows], FinancialReportPeriod.Create(2026, 10), Coverage)!;
        Assert.Equal("MAP", octLine.Service);
        Assert.Equal(100_000m, octLine.CashReceipts);
        Assert.Equal(100_000m, octLine.DepositsHeld);
        Assert.Equal(0m, octLine.PublishedServiceValue);
        Assert.False(octLine.PublishedInPeriod);

        var novLine = FinancialReportCalculator.Calculate([.. rows], FinancialReportPeriod.Create(2026, 11), Coverage)!;
        Assert.Equal(150_000m, novLine.PublishedServiceValue);
        Assert.Equal(50_000m, novLine.CashReceipts);
        Assert.Equal(0m, novLine.DepositsHeld);
        Assert.Equal(0m, novLine.OutstandingBalance);
        Assert.Equal(50_000m, novLine.PendingPayments);
        Assert.True(novLine.PublishedInPeriod);

        var decLine = FinancialReportCalculator.Calculate([.. rows], FinancialReportPeriod.Create(2026, 12), Coverage)!;
        Assert.Equal(0m, decLine.PublishedServiceValue);
        Assert.Equal(20_000m, decLine.ServiceRefunds);
        Assert.Equal(20_000m, decLine.ComplaintRefunds);
        Assert.Equal(20_000m, decLine.CashRefunds);
        Assert.Equal(-20_000m, decLine.NetRevenue);
        Assert.Equal(-20_000m, decLine.CashReceipts - decLine.CashRefunds);
    }

    [Fact, Trait("TC", "TC-NFR-07")]
    public void ServiceFilterSeparatesMapAndComboOrdersCleanly()
    {
        var mapOrder = Guid.NewGuid();
        var comboOrder = Guid.NewGuid();
        var nov = FinancialReportPeriod.Create(2026, 11);

        var mapRows = new[]
        {
            Row(1, mapOrder, "survey_orders", new { order_number = "MAP-1", final_price = 150_000m, status = "COMPLETED", created_at = "2026-11-02T00:00:00Z" }),
            Row(2, mapOrder, "order_commercial_snapshots", new { service_code = "MAP" }),
            Row(3, mapOrder, "original_service_publication", new { amount = 150_000m, published_at = "2026-11-05T00:00:00Z" })
        };
        var comboRows = new[]
        {
            Row(1, comboOrder, "survey_orders", new { order_number = "COMBO-1", final_price = 300_000m, status = "COMPLETED", created_at = "2026-11-02T00:00:00Z" }),
            Row(2, comboOrder, "order_commercial_snapshots", new { service_code = "COMBO" }),
            Row(3, comboOrder, "original_service_publication", new { amount = 300_000m, published_at = "2026-11-06T00:00:00Z" })
        };

        var lines = new[]
        {
            FinancialReportCalculator.Calculate(mapRows, nov, Coverage)!,
            FinancialReportCalculator.Calculate(comboRows, nov, Coverage)!
        };

        Assert.Equal(150_000m, lines.Where(x => x.Service == "MAP").Sum(x => x.PublishedServiceValue));
        Assert.Equal(300_000m, lines.Where(x => x.Service == "COMBO").Sum(x => x.PublishedServiceValue));
        Assert.Equal(450_000m, lines.Sum(x => x.PublishedServiceValue));
    }

    private static FinancialHistoryRow Row(long seq, Guid orderId, string source, object data)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(data));
        return new FinancialHistoryRow(
            seq, source, Guid.NewGuid(), orderId,
            DateTimeOffset.Parse("2026-11-01T00:00:00Z", CultureInfo.InvariantCulture),
            doc.RootElement.Clone(), false);
    }
}
