using System.ComponentModel.DataAnnotations;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Surveys.Application.Abstractions.Results;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Api.Contracts.Surveys.V3;

public sealed record V3ComboFindingReviewDecisionRequest(
    [param: Required] Guid? FindingId,
    [param: Required, StringLength(32, MinimumLength = 1)] string Decision,
    [param: StringLength(500)] string? Reason = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null,
    Guid? ReviewedPlantProfileId = null);

public sealed record V3ComboZoneReviewDecisionRequest(
    [param: Required, StringLength(128, MinimumLength = 1)] string SourceProposalId,
    [param: Required, StringLength(32, MinimumLength = 1)] string Decision,
    GeoJsonPolygonV3? ReviewedGeometry = null,
    IReadOnlyList<Guid>? ReviewedFindingIds = null,
    Guid? SelectedGuidanceId = null,
    int? SelectedGuidanceVersionNumber = null,
    [param: StringLength(500)] string? Reason = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null);

public sealed record V3ComboBlockerResolutionRequest(
    [param: Required, StringLength(64, MinimumLength = 1)] string SourceType,
    [param: Required, StringLength(128, MinimumLength = 1)] string SourceId,
    [param: Required, StringLength(64, MinimumLength = 1)] string ReasonCode,
    [param: Required, StringLength(64, MinimumLength = 1)] string Resolution,
    [param: StringLength(500)] string? Reason = null,
    IReadOnlyList<Guid>? EvidenceMediaIds = null);

public sealed record RecordV3ComboReviewRequest(
    [param: Required] Guid? OperationId,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required] Guid? ExpectedBatchId,
    [param: Required, Range(1, int.MaxValue)] int? ExpectedSourceRevision,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedBatchFingerprint,
    Guid? ExpectedPreviousReviewId,
    [param: Required, StringLength(32, MinimumLength = 1)] string Outcome,
    [param: Required, StringLength(500, MinimumLength = 1)] string ReviewReason,
    [param: Required] IReadOnlyList<V3ComboFindingReviewDecisionRequest> FindingDecisions,
    [param: Required] IReadOnlyList<V3ComboZoneReviewDecisionRequest> ZoneDecisions,
    [param: Required] IReadOnlyList<V3ComboBlockerResolutionRequest> BlockerResolutions,
    bool ConfirmNoDiseaseObserved = false,
    [param: StringLength(500)] string? NegativeResultConfirmationEvidence = null);

public sealed record FreezeV3InternalResultRequest(
    [param: Required] Guid? OperationId,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required] Guid? ExpectedAttemptId,
    [param: Required] Guid? ExpectedCountSnapshotId,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedCountFingerprint,
    [param: Required, StringLength(64, MinimumLength = 1)] string ReviewDecision,
    [param: Required, StringLength(500, MinimumLength = 1)] string FreezeReason,
    Guid? ExpectedComboBatchId = null,
    int? ExpectedComboSourceRevision = null,
    [param: RegularExpression("^[0-9a-fA-F]{64}$")]
    string? ExpectedComboBatchFingerprint = null,
    Guid? ExpectedComboReviewRevisionId = null,
    [param: RegularExpression("^[0-9a-fA-F]{64}$")]
    string? ExpectedComboReviewFingerprint = null);

public sealed record FinalizeV3InvoiceRequest(
    [param: Required] Guid? OperationId,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required] Guid? ExpectedApprovedResultRevisionId,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedApprovedResultFingerprint,
    [param: Required] Guid? ExpectedCountSnapshotId,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedCountFingerprint,
    [param: Required, StringLength(500, MinimumLength = 1)] string FinalizationReason,
    [param: StringLength(64, MinimumLength = 1)] string? ZeroPoleReasonCode = null,
    [param: StringLength(500, MinimumLength = 1)] string? ZeroPoleEvidenceReference = null,
    GeoJsonPolygonV3? PlantHealthSurveyArea = null);

public sealed record PreReleaseV3PoleCountItemCorrectionRequest(
    [param: Required] Guid? MappingCandidateId,
    bool IsAccepted,
    [param: StringLength(500)] string? ExclusionReason = null,
    Guid? ExpectedPlantProfileId = null);

public sealed record V3ComboReviewDecisionsSnapshotRequest(
    [param: Required] IReadOnlyList<V3ComboFindingReviewDecisionRequest> FindingDecisions,
    [param: Required] IReadOnlyList<V3ComboZoneReviewDecisionRequest> ZoneDecisions,
    [param: Required] IReadOnlyList<V3ComboBlockerResolutionRequest> BlockerResolutions);

public sealed record PreReleaseV3ComboReviewRequest(
    [param: Required] V3ComboReviewDecisionsSnapshotRequest Decisions,
    bool ConfirmNoDiseaseObserved = false,
    [param: StringLength(500)] string? NegativeResultConfirmationEvidence = null);

public sealed record CorrectV3PreReleaseInvoiceRequest(
    [param: Required] Guid? OperationId,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required] Guid? ExpectedCurrentApprovedResultRevisionId,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedCurrentApprovedResultFingerprint,
    [param: Required] Guid? ExpectedCurrentInvoiceRevisionId,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedCurrentInvoiceFingerprint,
    [param: Required] Guid? ExpectedCurrentCountSnapshotId,
    [param: Required, RegularExpression("^[0-9a-fA-F]{64}$")]
    string ExpectedCurrentCountFingerprint,
    [param: Required, StringLength(64, MinimumLength = 1)] string ReviewDecision,
    [param: Required, StringLength(500, MinimumLength = 1)] string CorrectionReason,
    [param: Required, MinLength(1), MaxLength(10_000)]
    IReadOnlyList<PreReleaseV3PoleCountItemCorrectionRequest> ItemCorrections,
    [param: StringLength(64, MinimumLength = 1)] string? ZeroPoleReasonCode = null,
    [param: StringLength(500, MinimumLength = 1)] string? ZeroPoleEvidenceReference = null,
    PreReleaseV3ComboReviewRequest? ReplacementComboReview = null);

public sealed record V3SurveyResultReviewApiResponse(
    Guid OrderId,
    Guid TenantId,
    Guid FarmId,
    uint OrderVersion,
    string OrderStatus,
    string ServiceCode,
    bool ScreensDisease,
    string MappingKind,
    Guid CaptureAttemptId,
    uint CaptureAttemptVersion,
    string CaptureAttemptStatus,
    Guid MissionId,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid FarmBoundaryVersionId,
    Guid MappingCandidateBatchId,
    Guid VerifiedPoleCountSnapshotId,
    int VerifiedBillablePoleCount,
    string CountSnapshotFingerprint,
    int CountSnapshotRevision,
    Guid? CurrentApprovedResultRevisionId,
    string? CurrentApprovedResultFingerprint,
    SurveyComboPendingReviewBatch? ComboPendingBatch,
    SurveyComboReviewSummaryDto? CurrentComboReview,
    SurveyExpectedFreezeTokensDto ExpectedFreezeTokens,
    IReadOnlyList<ComboSnapTargetDto> ComboSnapTargets);

public sealed record V3ComboReviewApiResponse(
    Guid OrderId,
    Guid BatchId,
    int SourceRevision,
    Guid ReviewRevisionId,
    int ReviewRevisionNumber,
    Guid? PredecessorReviewId,
    string DecisionFingerprint,
    string Outcome,
    int UnresolvedBlockersCount,
    IReadOnlyList<string> UnresolvedBlockers,
    DateTimeOffset ReviewedAt,
    bool IsReplay);

public sealed record V3FreezeInternalResultApiResponse(
    Guid OrderId,
    uint OrderVersion,
    string OrderStatus,
    string ServiceCode,
    bool ScreensDisease,
    Guid ApprovedResultRevisionId,
    int ApprovedResultRevisionNumber,
    string ApprovedResultFingerprint,
    string ReviewDecision,
    string HealthAssessmentStatus,
    string? LimitationNotice,
    Guid AttemptId,
    uint AttemptVersion,
    string AttemptStatus,
    Guid MissionId,
    Guid CountSnapshotId,
    int VerifiedBillablePoleCount,
    string CountSnapshotFingerprint,
    Guid FarmBaseMapVersionId,
    Guid BaselineMapVersionId,
    Guid FarmBoundaryVersionId,
    Guid MappingCandidateBatchId,
    Guid? ComboBatchId,
    string? ComboBatchFingerprint,
    Guid? ComboReviewRevisionId,
    string? ComboReviewFingerprint,
    DateTimeOffset FrozenAt,
    bool IsReplay);

public sealed record V3ManagerInvoiceApiResponse(
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
    decimal OrderAvailableRefundCap = 0m,
    V3ComboInvoicePricingApiResponse? ComboPricing = null);

public sealed record V3ComboInvoicePricingApiResponse(
    int MappingPoleCount,
    decimal MappingPricePerPole,
    decimal MappingSubtotal,
    int PlantHealthPoleCount,
    decimal PlantHealthPricePerPole,
    decimal PlantHealthSubtotal);

public sealed record V3FinalizeInvoiceApiResponse(
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
    bool IsReplay);

public sealed record V3CorrectPreReleaseInvoiceApiResponse(
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
    bool IsReplay,
    Guid? ComboReviewRevisionId,
    string? ComboReviewFingerprint);

public sealed record InitiateV3BalanceCheckoutRequest(
    [param: Required, StringLength(8192, MinimumLength = 1)]
    string CheckoutToken,
    Guid IdempotencyKey);

public sealed record V3BalanceCheckoutApiResponse(
    Guid OrderId,
    Guid PaymentId,
    Guid InvoiceRevisionId,
    int? InvoiceRevisionNumber,
    decimal Amount,
    string Currency,
    string Provider,
    string? ProviderReference,
    string PaymentStatus,
    Uri? RedirectUrl,
    string? ClientToken,
    DateTimeOffset? ExpiresAt,
    uint? PaymentVersion,
    uint? OrderVersion,
    bool IsReplay);

public sealed record ReconcileV3SurveyOrderFundingRequest(
    [param: Required] Guid? OperationId,
    [param: Required] SurveyReconciliationDispositionKind? DispositionKind,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required]
    [param: Range(typeof(decimal), "0", "79228162514264337593543950335")]
    decimal? Amount,
    [param: Required, StringLength(3, MinimumLength = 3)]
    string Currency,
    [param: Required, StringLength(2000, MinimumLength = 1)]
    string Reason,
    [param: Required, StringLength(1000, MinimumLength = 1)]
    string EvidenceReference,
    Guid? SurveyPaymentId = null,
    Guid? FundingReceiptId = null,
    Guid? TargetInvoiceRevisionId = null,
    [param: StringLength(150)] string? ProviderReference = null,
    [param: StringLength(150)] string? ProviderTransactionId = null,
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedPaymentVersion = null);

public sealed record V3FundingReconciliationApiResponse(
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

public sealed record V3CaptureAttemptHistoryApiResponse(
    uint TransitionSequence,
    string Action,
    CaptureAttemptStatus? FromStatus,
    CaptureAttemptStatus ToStatus,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record V3CaptureAttemptApiResponse(
    Guid CaptureAttemptId,
    int AttemptIndex,
    CaptureAttemptStatus Status,
    uint Version,
    Guid? MissionId,
    Guid? ParentAttemptId,
    Guid? ParentMissionId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyList<V3CaptureAttemptHistoryApiResponse> History,
    Guid? ManifestId = null,
    string? ManifestFingerprint = null,
    Guid? ProcessingJobId = null,
    uint? OrderVersion = null,
    Guid? MappingSourceAttemptId = null);

public sealed record V3TenantOwnerInvoiceApiResponse(
    Guid OrderId,
    string OrderNumber,
    string OrderStatus,
    int InvoiceRevisionNumber,
    int VerifiedBillablePoleCount,
    decimal PricePerPole,
    string Currency,
    decimal TotalAmount,
    decimal DueAmount,
    DateTimeOffset FinalizedAt,
    string? CheckoutToken = null,
    V3ComboInvoicePricingApiResponse? ComboPricing = null);
