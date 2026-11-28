using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.CorrectSurveyOrderPreReleaseInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.FinalizeSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetManagerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetManagerSurveyOrderCaptureAttempts;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateBalanceCheckout;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Results.FreezeSurveyInternalResult;
using AgriDrone.Modules.Surveys.Application.Features.Results.GetSurveyResultReview;
using AgriDrone.Modules.Surveys.Application.Features.Results.RecordSurveyComboReview;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Api.Mapping;

internal static class V3SurveyResultAndInvoiceMapper
{
    public static RecordSurveyComboReviewCommand ToCommand(
        Guid orderId,
        RecordV3ComboReviewRequest request) => new(
            OrderId: orderId,
            OperationId: request.OperationId!.Value,
            ExpectedOrderVersion: request.ExpectedOrderVersion!.Value,
            ExpectedBatchId: request.ExpectedBatchId!.Value,
            ExpectedSourceRevision: request.ExpectedSourceRevision!.Value,
            ExpectedBatchFingerprint: request.ExpectedBatchFingerprint,
            ExpectedPreviousReviewId: request.ExpectedPreviousReviewId,
            Outcome: request.Outcome,
            ReviewReason: request.ReviewReason,
            FindingDecisions: request.FindingDecisions
                .Select(f => new ComboFindingReviewDecisionInput(
                    f.FindingId!.Value,
                    f.Decision,
                    f.Reason,
                    f.EvidenceMediaIds,
                    f.ReviewedPlantProfileId))
                .ToArray(),
            ZoneDecisions: request.ZoneDecisions
                .Select(z => new ComboZoneReviewDecisionInput(
                    z.SourceProposalId,
                    z.Decision,
                    z.ReviewedGeometry,
                    z.ReviewedFindingIds,
                    z.SelectedGuidanceId,
                    z.SelectedGuidanceVersionNumber,
                    z.Reason,
                    z.EvidenceMediaIds))
                .ToArray(),
            BlockerResolutions: request.BlockerResolutions
                .Select(b => new ComboBlockerResolutionInput(
                    b.SourceType,
                    b.SourceId,
                    b.ReasonCode,
                    b.Resolution,
                    b.Reason,
                    b.EvidenceMediaIds))
                .ToArray(),
            ConfirmNoDiseaseObserved: request.ConfirmNoDiseaseObserved,
            NegativeResultConfirmationEvidence: request.NegativeResultConfirmationEvidence);

    public static FreezeSurveyInternalResultCommand ToCommand(
        Guid orderId,
        FreezeV3InternalResultRequest request) => new(
            OrderId: orderId,
            OperationId: request.OperationId!.Value,
            ExpectedOrderVersion: request.ExpectedOrderVersion!.Value,
            ExpectedAttemptId: request.ExpectedAttemptId!.Value,
            ExpectedCountSnapshotId: request.ExpectedCountSnapshotId!.Value,
            ExpectedCountFingerprint: request.ExpectedCountFingerprint,
            ReviewDecision: request.ReviewDecision,
            FreezeReason: request.FreezeReason,
            ExpectedComboBatchId: request.ExpectedComboBatchId,
            ExpectedComboSourceRevision: request.ExpectedComboSourceRevision,
            ExpectedComboBatchFingerprint: request.ExpectedComboBatchFingerprint,
            ExpectedComboReviewRevisionId: request.ExpectedComboReviewRevisionId,
            ExpectedComboReviewFingerprint: request.ExpectedComboReviewFingerprint);

    public static FinalizeSurveyOrderInvoiceCommand ToCommand(
        Guid orderId,
        FinalizeV3InvoiceRequest request) => new(
            OrderId: orderId,
            OperationId: request.OperationId!.Value,
            ExpectedOrderVersion: request.ExpectedOrderVersion!.Value,
            ExpectedApprovedResultRevisionId: request.ExpectedApprovedResultRevisionId!.Value,
            ExpectedApprovedResultFingerprint: request.ExpectedApprovedResultFingerprint,
            ExpectedCountSnapshotId: request.ExpectedCountSnapshotId!.Value,
            ExpectedCountFingerprint: request.ExpectedCountFingerprint,
            FinalizationReason: request.FinalizationReason,
            ZeroPoleReasonCode: request.ZeroPoleReasonCode,
            ZeroPoleEvidenceReference: request.ZeroPoleEvidenceReference);

    public static CorrectSurveyOrderPreReleaseInvoiceCommand ToCommand(
        Guid orderId,
        CorrectV3PreReleaseInvoiceRequest request) => new(
            OrderId: orderId,
            OperationId: request.OperationId!.Value,
            ExpectedOrderVersion: request.ExpectedOrderVersion!.Value,
            ExpectedCurrentApprovedResultRevisionId: request.ExpectedCurrentApprovedResultRevisionId!.Value,
            ExpectedCurrentApprovedResultFingerprint: request.ExpectedCurrentApprovedResultFingerprint,
            ExpectedCurrentInvoiceRevisionId: request.ExpectedCurrentInvoiceRevisionId!.Value,
            ExpectedCurrentInvoiceFingerprint: request.ExpectedCurrentInvoiceFingerprint,
            ExpectedCurrentCountSnapshotId: request.ExpectedCurrentCountSnapshotId!.Value,
            ExpectedCurrentCountFingerprint: request.ExpectedCurrentCountFingerprint,
            ReviewDecision: request.ReviewDecision,
            CorrectionReason: request.CorrectionReason,
            ItemCorrections: request.ItemCorrections
                .Select(i => new PreReleasePoleCountItemCorrection(
                    i.MappingCandidateId!.Value,
                    i.IsAccepted,
                    i.ExclusionReason,
                    i.ExpectedPlantProfileId))
                .ToArray(),
            ZeroPoleReasonCode: request.ZeroPoleReasonCode,
            ZeroPoleEvidenceReference: request.ZeroPoleEvidenceReference,
            ReplacementComboReview: request.ReplacementComboReview is null
                ? null
                : new PreReleaseComboReview(
                    Decisions: new ComboReviewDecisionsSnapshotRecord(
                        FindingDecisions: request.ReplacementComboReview.Decisions.FindingDecisions
                            .Select(f => new ComboFindingReviewDecisionRecord(
                                f.FindingId!.Value,
                                f.Decision,
                                f.Reason,
                                f.EvidenceMediaIds ?? [],
                                f.ReviewedPlantProfileId))
                            .ToArray(),
                        ZoneDecisions: request.ReplacementComboReview.Decisions.ZoneDecisions
                            .Select(z => new ComboZoneReviewDecisionRecord(
                                z.SourceProposalId,
                                z.Decision,
                                z.ReviewedGeometry,
                                z.ReviewedFindingIds,
                                z.SelectedGuidanceId,
                                z.SelectedGuidanceVersionNumber,
                                z.Reason,
                                z.EvidenceMediaIds ?? []))
                            .ToArray(),
                        BlockerResolutions: request.ReplacementComboReview.Decisions.BlockerResolutions
                            .Select(b => new ComboBlockerResolutionRecord(
                                b.SourceType,
                                b.SourceId,
                                b.ReasonCode,
                                b.Resolution,
                                b.Reason,
                                b.EvidenceMediaIds ?? []))
                            .ToArray()),
                    ConfirmNoDiseaseObserved: request.ReplacementComboReview.ConfirmNoDiseaseObserved,
                    NegativeResultConfirmationEvidence: request.ReplacementComboReview.NegativeResultConfirmationEvidence));

    public static V3SurveyResultReviewApiResponse ToResponse(
        SurveyResultReviewResponse response) => new(
            response.OrderId,
            response.TenantId,
            response.FarmId,
            response.OrderVersion,
            response.OrderStatus,
            response.ServiceCode,
            response.ScreensDisease,
            response.MappingKind,
            response.CaptureAttemptId,
            response.CaptureAttemptVersion,
            response.CaptureAttemptStatus,
            response.MissionId,
            response.FarmBaseMapVersionId,
            response.BaselineMapVersionId,
            response.FarmBoundaryVersionId,
            response.MappingCandidateBatchId,
            response.VerifiedPoleCountSnapshotId,
            response.VerifiedBillablePoleCount,
            response.CountSnapshotFingerprint,
            response.CountSnapshotRevision,
            response.CurrentApprovedResultRevisionId,
            response.CurrentApprovedResultFingerprint,
            response.ComboPendingBatch,
            response.CurrentComboReview,
            response.ExpectedFreezeTokens,
            response.ComboSnapTargets ?? []);

    public static V3ComboReviewApiResponse ToResponse(
        RecordSurveyComboReviewResponse response) => new(
            response.OrderId,
            response.BatchId,
            response.SourceRevision,
            response.ReviewRevisionId,
            response.ReviewRevisionNumber,
            response.PredecessorReviewId,
            response.DecisionFingerprint,
            response.Outcome,
            response.UnresolvedBlockersCount,
            response.UnresolvedBlockers,
            response.ReviewedAt,
            response.IsReplay);

    public static V3FreezeInternalResultApiResponse ToResponse(
        FreezeSurveyInternalResultResponse response) => new(
            response.OrderId,
            response.OrderVersion,
            response.OrderStatus,
            response.ServiceCode,
            response.ScreensDisease,
            response.ApprovedResultRevisionId,
            response.ApprovedResultRevisionNumber,
            response.ApprovedResultFingerprint,
            response.ReviewDecision,
            response.HealthAssessmentStatus,
            response.LimitationNotice,
            response.AttemptId,
            response.AttemptVersion,
            response.AttemptStatus,
            response.MissionId,
            response.CountSnapshotId,
            response.VerifiedBillablePoleCount,
            response.CountSnapshotFingerprint,
            response.FarmBaseMapVersionId,
            response.BaselineMapVersionId,
            response.FarmBoundaryVersionId,
            response.MappingCandidateBatchId,
            response.ComboBatchId,
            response.ComboBatchFingerprint,
            response.ComboReviewRevisionId,
            response.ComboReviewFingerprint,
            response.FrozenAt,
            response.IsReplay);

    public static V3ManagerInvoiceApiResponse ToResponse(
        ManagerSurveyOrderInvoiceResponse response) => new(
            response.OrderId,
            response.TenantId,
            response.FarmId,
            response.OrderNumber,
            response.OrderVersion,
            response.OrderStatus,
            response.Scope,
            response.ServiceCode,
            response.ScreensDisease,
            response.CurrentInvoiceRevisionId,
            response.InvoiceRevisionNumber,
            response.InvoiceFingerprint,
            response.CurrentApprovedResultRevisionId,
            response.ApprovedResultRevisionNumber,
            response.ApprovedResultFingerprint,
            response.VerifiedPoleCountSnapshotId,
            response.CountSnapshotRevisionNumber,
            response.CountSnapshotFingerprint,
            response.SurveyServicePriceId,
            response.PricePerPole,
            response.Currency,
            response.CommercialPolicyVersionId,
            response.VerifiedBillablePoleCount,
            response.TotalAmount,
            response.ValidAllocatedDepositNet,
            response.ConfirmedNetReceipts,
            response.DueAmount,
            response.OverpaymentAmount,
            response.ZeroPoleApprovedBy,
            response.ZeroPoleReasonCode,
            response.ZeroPoleEvidenceReference,
            response.ComboBatchId,
            response.ComboReviewRevisionId,
            response.ComboReviewFingerprint,
            response.OpenFinalBalanceCheckoutPaymentIds,
            response.FinalizedAt,
            response.HasOpenReconciliation,
            response.ReconciliationReasons,
            response.OrderConfirmedNetCash,
            response.OrderAvailableRefundCap);

    public static V3FinalizeInvoiceApiResponse ToResponse(
        FinalizeSurveyOrderInvoiceResponse response) => new(
            response.OrderId,
            response.OrderVersion,
            response.OrderStatus,
            response.ServiceCode,
            response.ScreensDisease,
            response.InvoiceRevisionId,
            response.InvoiceRevisionNumber,
            response.InvoiceFingerprint,
            response.ApprovedResultRevisionId,
            response.ApprovedResultFingerprint,
            response.CountSnapshotId,
            response.CountSnapshotFingerprint,
            response.VerifiedBillablePoleCount,
            response.SurveyServicePriceId,
            response.PricePerPole,
            response.Currency,
            response.CommercialPolicyVersionId,
            response.TotalAmount,
            response.ValidAllocatedDepositNet,
            response.ConfirmedNetReceipts,
            response.DueAmount,
            response.OverpaymentAmount,
            response.ZeroPoleApprovedBy,
            response.ZeroPoleReasonCode,
            response.ZeroPoleEvidenceReference,
            response.FinalizedAt,
            response.IsReplay);

    public static V3CorrectPreReleaseInvoiceApiResponse ToResponse(
        CorrectSurveyOrderPreReleaseInvoiceResponse response) => new(
            response.OrderId,
            response.OrderVersion,
            response.OrderStatus,
            response.ServiceCode,
            response.ScreensDisease,
            response.PreviousCountSnapshotId,
            response.NewCountSnapshotId,
            response.NewCountSnapshotRevisionNumber,
            response.NewCountSnapshotFingerprint,
            response.PreviousVerifiedBillablePoleCount,
            response.NewVerifiedBillablePoleCount,
            response.PreviousApprovedResultRevisionId,
            response.NewApprovedResultRevisionId,
            response.NewApprovedResultRevisionNumber,
            response.NewApprovedResultFingerprint,
            response.PreviousInvoiceRevisionId,
            response.NewInvoiceRevisionId,
            response.NewInvoiceRevisionNumber,
            response.NewInvoiceFingerprint,
            response.SurveyServicePriceId,
            response.PricePerPole,
            response.Currency,
            response.CommercialPolicyVersionId,
            response.TotalAmount,
            response.ValidAllocatedDepositNet,
            response.ConfirmedNetReceipts,
            response.DueAmount,
            response.OverpaymentAmount,
            response.InvalidatedCheckoutPaymentIds,
            response.ZeroPoleApprovedBy,
            response.ZeroPoleReasonCode,
            response.ZeroPoleEvidenceReference,
            response.CorrectedAt,
            response.IsReplay,
            response.ComboReviewRevisionId,
            response.ComboReviewFingerprint);

    public static V3TenantOwnerInvoiceApiResponse ToResponse(
        TenantOwnerSurveyOrderInvoiceResponse response) => new(
            response.OrderId,
            response.OrderNumber,
            response.OrderStatus,
            response.InvoiceRevisionNumber,
            response.VerifiedBillablePoleCount,
            response.PricePerPole,
            response.Currency,
            response.TotalAmount,
            response.DueAmount,
            response.FinalizedAt,
            response.CheckoutToken);

    public static V3BalanceCheckoutApiResponse ToResponse(
        InitiateBalanceCheckoutResponse response) => new(
            response.OrderId,
            response.PaymentId,
            response.InvoiceRevisionId,
            response.InvoiceRevisionNumber,
            response.Amount,
            response.Currency,
            response.Provider,
            response.ProviderReference,
            response.PaymentStatus,
            response.RedirectUrl,
            response.ClientToken,
            response.ExpiresAt,
            response.PaymentVersion,
            response.OrderVersion,
            response.IsReplay);

    public static ReconcileV3SurveyOrderFundingCommand ToCommand(
        Guid orderId,
        ReconcileV3SurveyOrderFundingRequest request) => new(
            OrderId: orderId,
            OperationId: request.OperationId!.Value,
            DispositionKind: request.DispositionKind!.Value,
            SurveyPaymentId: request.SurveyPaymentId,
            FundingReceiptId: request.FundingReceiptId,
            TargetInvoiceRevisionId: request.TargetInvoiceRevisionId,
            Amount: request.Amount!.Value,
            Currency: request.Currency,
            ProviderReference: request.ProviderReference,
            ProviderTransactionId: request.ProviderTransactionId,
            Reason: request.Reason,
            EvidenceReference: request.EvidenceReference,
            ExpectedOrderVersion: request.ExpectedOrderVersion!.Value,
            ExpectedPaymentVersion: request.ExpectedPaymentVersion);

    public static V3FundingReconciliationApiResponse ToResponse(
        V3FundingReconciliationResult result) => new(
            DecisionId: result.DecisionId,
            OrderId: result.OrderId,
            OperationId: result.OperationId,
            DispositionKind: result.DispositionKind,
            SurveyPaymentId: result.SurveyPaymentId,
            FundingReceiptId: result.FundingReceiptId,
            TargetInvoiceRevisionId: result.TargetInvoiceRevisionId,
            Amount: result.Amount,
            Currency: result.Currency,
            ProviderReference: result.ProviderReference,
            ProviderTransactionId: result.ProviderTransactionId,
            PaymentStatus: result.PaymentStatus,
            OrderStatus: result.OrderStatus,
            PaymentVersion: result.PaymentVersion,
            OrderVersion: result.OrderVersion,
            HasOpenReconciliation: result.HasOpenReconciliation,
            ConfirmedNetReceipts: result.ConfirmedNetReceipts,
            OrderConfirmedNetCash: result.OrderConfirmedNetCash,
            AvailableRefundCap: result.AvailableRefundCap,
            ReconciledBy: result.ReconciledBy,
            ReconciledAt: result.ReconciledAt,
            IsReplay: result.IsReplay);

    public static IReadOnlyList<V3CaptureAttemptApiResponse> ToResponse(
        IReadOnlyList<ManagerSurveyOrderCaptureAttemptResponse> items) =>
        items.Select(attempt => new V3CaptureAttemptApiResponse(
                CaptureAttemptId: attempt.CaptureAttemptId,
                AttemptIndex: attempt.AttemptIndex,
                Status: attempt.Status,
                Version: attempt.Version,
                MissionId: attempt.MissionId,
                ParentAttemptId: attempt.ParentAttemptId,
                ParentMissionId: attempt.ParentMissionId,
                StartedAt: attempt.StartedAt,
                EndedAt: attempt.EndedAt,
                History: attempt.History
                    .Select(history => new V3CaptureAttemptHistoryApiResponse(
                        TransitionSequence: history.TransitionSequence,
                        Action: history.Action,
                        FromStatus: history.FromStatus,
                        ToStatus: history.ToStatus,
                        Reason: history.Reason,
                        CreatedAt: history.CreatedAt))
                    .ToArray(),
                ManifestId: attempt.ManifestId,
                ManifestFingerprint: attempt.ManifestFingerprint,
                ProcessingJobId: attempt.ProcessingJobId,
                OrderVersion: attempt.OrderVersion,
                MappingSourceAttemptId: attempt.MappingSourceAttemptId))
            .ToArray();
}
