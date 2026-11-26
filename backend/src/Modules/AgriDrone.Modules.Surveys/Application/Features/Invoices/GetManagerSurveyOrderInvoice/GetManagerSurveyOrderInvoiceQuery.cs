using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;

public sealed record GetManagerSurveyOrderInvoiceQuery(Guid OrderId)
    : IRequest<Result<ManagerSurveyOrderInvoiceResponse>>;

public sealed record ManagerSurveyOrderInvoiceResponse(
    Guid OrderId,
    Guid TenantId,
    Guid FarmId,
    string OrderNumber,
    uint OrderVersion,
    string OrderStatus,
    string Scope,
    string ServiceCode,
    bool ScreensDisease,
    Guid CurrentInvoiceRevisionId,
    int InvoiceRevisionNumber,
    string InvoiceFingerprint,
    Guid CurrentApprovedResultRevisionId,
    int ApprovedResultRevisionNumber,
    string ApprovedResultFingerprint,
    Guid VerifiedPoleCountSnapshotId,
    int CountSnapshotRevisionNumber,
    string CountSnapshotFingerprint,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency,
    Guid CommercialPolicyVersionId,
    int VerifiedBillablePoleCount,
    decimal TotalAmount,
    decimal ValidAllocatedDepositNet,
    decimal ConfirmedNetReceipts,
    decimal DueAmount,
    decimal OverpaymentAmount,
    Guid? ZeroPoleApprovedBy,
    string? ZeroPoleReasonCode,
    string? ZeroPoleEvidenceReference,
    Guid? ComboBatchId,
    Guid? ComboReviewRevisionId,
    string? ComboReviewFingerprint,
    IReadOnlyList<Guid> OpenFinalBalanceCheckoutPaymentIds,
    DateTimeOffset FinalizedAt,
    bool HasOpenReconciliation = false,
    IReadOnlyList<string>? ReconciliationReasons = null,
    decimal OrderConfirmedNetCash = 0m,
    decimal OrderAvailableRefundCap = 0m);
