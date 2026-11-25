namespace AgriDrone.Modules.Surveys.Application.Features.Reports;

public sealed record FinancialReportLine(
    Guid OrderId, string OrderNumber, string Service, decimal PublishedServiceValue,
    decimal ServiceRefunds, decimal CashReceipts, decimal CashRefunds, decimal DepositsHeld,
    decimal RefundableExcess, decimal RefundPayable, decimal CancellationRetention,
    decimal OutstandingBalance, decimal PendingPayments, bool PublishedInPeriod, bool Unpriced, bool NeedsReconciliation)
{
    public decimal OverpaymentRefunds { get; init; }
    public decimal CancellationRefunds { get; init; }
    public decimal ServiceFailureRefunds { get; init; }
    public decimal ComplaintRefunds { get; init; }
    public decimal UnclassifiedRefunds { get; init; }
    public decimal CompletedRefunds =>
        OverpaymentRefunds + CancellationRefunds + ServiceFailureRefunds + ComplaintRefunds + UnclassifiedRefunds;
    public decimal FundingReversals => CashRefunds - CompletedRefunds;
    public decimal NetRevenue => PublishedServiceValue - CompletedRefunds;
}
