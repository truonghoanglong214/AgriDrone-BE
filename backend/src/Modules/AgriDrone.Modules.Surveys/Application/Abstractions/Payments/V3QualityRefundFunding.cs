namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public sealed record V3QualityRefundFunding(
    decimal GrossConfirmedReceipts,
    decimal CompletedRefunds,
    decimal OrderConfirmedNetCash,
    decimal AvailableRefundCap,
    bool RequiresReconciliation);
