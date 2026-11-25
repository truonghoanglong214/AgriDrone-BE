using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.FinalizeSurveyOrderInvoice;

public sealed record FinalizeSurveyOrderInvoiceCommand(
    Guid OrderId,
    Guid OperationId,
    uint ExpectedOrderVersion,
    Guid ExpectedApprovedResultRevisionId,
    string ExpectedApprovedResultFingerprint,
    Guid ExpectedCountSnapshotId,
    string ExpectedCountFingerprint,
    string FinalizationReason,
    string? ZeroPoleReasonCode = null,
    string? ZeroPoleEvidenceReference = null)
    : IRequest<Result<FinalizeSurveyOrderInvoiceResponse>>;

public sealed record FinalizeSurveyOrderInvoiceResponse(
    Guid OrderId,
    uint OrderVersion,
    string OrderStatus,
    string ServiceCode,
    bool ScreensDisease,
    Guid InvoiceRevisionId,
    int InvoiceRevisionNumber,
    string InvoiceFingerprint,
    Guid ApprovedResultRevisionId,
    string ApprovedResultFingerprint,
    Guid CountSnapshotId,
    string CountSnapshotFingerprint,
    int VerifiedBillablePoleCount,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency,
    Guid CommercialPolicyVersionId,
    decimal TotalAmount,
    decimal ValidAllocatedDepositNet,
    decimal ConfirmedNetReceipts,
    decimal DueAmount,
    decimal OverpaymentAmount,
    Guid? ZeroPoleApprovedBy,
    string? ZeroPoleReasonCode,
    string? ZeroPoleEvidenceReference,
    DateTimeOffset FinalizedAt,
    bool IsReplay = false);
