using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface IV3FundingReconciliationTransaction
{
    Task<Result<V3FundingReconciliationResult>> ReconcileAsync(
        V3FundingReconciliationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record V3FundingReconciliationRequest(
    Guid OrderId,
    Guid ActorId,
    Guid CorrelationId,
    Guid OperationId,
    SurveyReconciliationDispositionKind DispositionKind,
    Guid? SurveyPaymentId,
    Guid? FundingReceiptId,
    Guid? TargetInvoiceRevisionId,
    decimal Amount,
    string Currency,
    string? ProviderReference,
    string? ProviderTransactionId,
    string Reason,
    string EvidenceReference,
    uint ExpectedOrderVersion,
    uint? ExpectedPaymentVersion = null);

public sealed record V3FundingReconciliationResult(
    Guid DecisionId,
    Guid OrderId,
    Guid OperationId,
    SurveyReconciliationDispositionKind DispositionKind,
    Guid? SurveyPaymentId,
    Guid? FundingReceiptId,
    Guid? TargetInvoiceRevisionId,
    decimal Amount,
    string Currency,
    string? ProviderReference,
    string? ProviderTransactionId,
    string? PaymentStatus,
    string OrderStatus,
    uint? PaymentVersion,
    uint OrderVersion,
    bool HasOpenReconciliation,
    decimal ConfirmedNetReceipts,
    decimal OrderConfirmedNetCash,
    decimal AvailableRefundCap,
    Guid ReconciledBy,
    DateTimeOffset ReconciledAt,
    bool IsReplay);
