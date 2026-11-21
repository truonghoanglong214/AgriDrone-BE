namespace AgriDrone.Modules.Surveys.Application.Features.Reports;

public sealed record FinancialReportLine(
    Guid OrderId, string OrderNumber, string Service, decimal PublishedServiceValue,
    decimal ServiceRefunds, decimal CashReceipts, decimal CashRefunds, decimal DepositsHeld,
    decimal RefundableExcess, decimal RefundPayable, decimal CancellationRetention,
    decimal OutstandingBalance, decimal PendingPayments, bool PublishedInPeriod, bool Unpriced, bool NeedsReconciliation);
