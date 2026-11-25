using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Invoices.CorrectSurveyOrderPreReleaseInvoice;

public sealed record PreReleasePoleCountItemCorrection(
    Guid MappingCandidateId,
    bool IsAccepted,
    string? ExclusionReason = null,
    Guid? ExpectedPlantProfileId = null);

public sealed record PreReleaseComboReview(
    ComboReviewDecisionsSnapshotRecord Decisions,
    bool ConfirmNoDiseaseObserved = false,
    string? NegativeResultConfirmationEvidence = null);

public sealed record CorrectSurveyOrderPreReleaseInvoiceCommand(
    Guid OrderId,
    Guid OperationId,
    uint ExpectedOrderVersion,
    Guid ExpectedCurrentApprovedResultRevisionId,
    string ExpectedCurrentApprovedResultFingerprint,
    Guid ExpectedCurrentInvoiceRevisionId,
    string ExpectedCurrentInvoiceFingerprint,
    Guid ExpectedCurrentCountSnapshotId,
    string ExpectedCurrentCountFingerprint,
    string ReviewDecision,
    string CorrectionReason,
    IReadOnlyList<PreReleasePoleCountItemCorrection> ItemCorrections,
    string? ZeroPoleReasonCode = null,
    string? ZeroPoleEvidenceReference = null,
    PreReleaseComboReview? ReplacementComboReview = null)
    : IRequest<Result<CorrectSurveyOrderPreReleaseInvoiceResponse>>;

public sealed record CorrectSurveyOrderPreReleaseInvoiceResponse(
    Guid OrderId,
    uint OrderVersion,
    string OrderStatus,
    string ServiceCode,
    bool ScreensDisease,
    Guid PreviousCountSnapshotId,
    Guid NewCountSnapshotId,
    int NewCountSnapshotRevisionNumber,
    string NewCountSnapshotFingerprint,
    int PreviousVerifiedBillablePoleCount,
    int NewVerifiedBillablePoleCount,
    Guid PreviousApprovedResultRevisionId,
    Guid NewApprovedResultRevisionId,
    int NewApprovedResultRevisionNumber,
    string NewApprovedResultFingerprint,
    Guid PreviousInvoiceRevisionId,
    Guid NewInvoiceRevisionId,
    int NewInvoiceRevisionNumber,
    string NewInvoiceFingerprint,
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency,
    Guid CommercialPolicyVersionId,
    decimal TotalAmount,
    decimal ValidAllocatedDepositNet,
    decimal ConfirmedNetReceipts,
    decimal DueAmount,
    decimal OverpaymentAmount,
    IReadOnlyList<Guid> InvalidatedCheckoutPaymentIds,
    Guid? ZeroPoleApprovedBy,
    string? ZeroPoleReasonCode,
    string? ZeroPoleEvidenceReference,
    DateTimeOffset CorrectedAt,
    bool IsReplay = false,
    Guid? ComboReviewRevisionId = null,
    string? ComboReviewFingerprint = null);
