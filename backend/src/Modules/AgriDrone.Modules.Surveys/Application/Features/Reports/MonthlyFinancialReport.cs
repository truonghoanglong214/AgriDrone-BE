using System.Security.Cryptography;
using System.Text.Json;

namespace AgriDrone.Modules.Surveys.Application.Features.Reports;

public sealed record MonthlyFinancialReport(
    int Year, int Month, string? Service, string FormulaVersion, DateTimeOffset StartUtc,
    DateTimeOffset EndUtc, DateTimeOffset GeneratedAt, DateTimeOffset HistoryAvailableFrom,
    bool HistoricalBalancesComplete, IReadOnlyList<FinancialReportLine> Lines)
{
    public Guid? SnapshotId { get; init; }
    public DateTimeOffset? SnapshotExpiresAt { get; init; }
    public DateTimeOffset AsOfUtc => GeneratedAt < EndUtc ? GeneratedAt : EndUtc;
    public string ReportFingerprint => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Year, Month, Service, FormulaVersion, StartUtc, EndUtc, AsOfUtc, HistoryAvailableFrom,
        HistoricalBalancesComplete, Lines
    })));
    public decimal PendingPayments => Lines.Sum(x => x.PendingPayments);
    public decimal PublishedServiceValue => Lines.Sum(x => x.PublishedServiceValue);
    public decimal ServiceRefunds => Lines.Sum(x => x.ServiceRefunds);
    public decimal NetServiceRevenue => PublishedServiceValue - ServiceRefunds;
    public decimal OverpaymentRefunds => Lines.Sum(x => x.OverpaymentRefunds);
    public decimal CancellationRefunds => Lines.Sum(x => x.CancellationRefunds);
    public decimal ServiceFailureRefunds => Lines.Sum(x => x.ServiceFailureRefunds);
    public decimal ComplaintRefunds => Lines.Sum(x => x.ComplaintRefunds);
    public decimal UnclassifiedRefunds => Lines.Sum(x => x.UnclassifiedRefunds);
    public decimal CompletedRefunds => Lines.Sum(x => x.CompletedRefunds);
    public decimal NetRevenue => PublishedServiceValue - CompletedRefunds;
    public decimal CashReceipts => Lines.Sum(x => x.CashReceipts);
    public decimal CashRefunds => Lines.Sum(x => x.CashRefunds);
    public decimal FundingReversals => CashRefunds - CompletedRefunds;
    public decimal NetCash => CashReceipts - CashRefunds;
    public decimal DepositsHeld => Lines.Sum(x => x.DepositsHeld);
    public decimal RefundableExcess => Lines.Sum(x => x.RefundableExcess);
    public decimal RefundPayable => Lines.Sum(x => x.RefundPayable);
    public decimal CancellationRetention => Lines.Sum(x => x.CancellationRetention);
    public decimal OutstandingBalances => Lines.Sum(x => x.OutstandingBalance);
    public int PublishedOrders => Lines.Count(x => x.PublishedInPeriod);
    public int UnpricedOrders => Lines.Count(x => x.Unpriced);
    public int ReconciliationOrders => Lines.Count(x => x.NeedsReconciliation);
}
