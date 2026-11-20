using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyCancellationDecision : Entity
{
    private SurveyCancellationDecision() { }

    public Guid SurveyOrderId { get; private set; }
    public uint OrderVersion { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid DecisionActorId { get; private set; }
    public Guid CancellationPolicyId { get; private set; }
    public int CancellationPolicyVersion { get; private set; }
    public CancellationDecisionKind DecisionKind { get; private set; }
    public bool RefundableBeforeCapture { get; private set; }
    public bool RetainDepositAfterCapture { get; private set; }
    public int AllowedFreeNoShowReschedules { get; private set; }
    public bool OperationalPostponementRefundable { get; private set; }
    public bool ServiceFailureRefundable { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string ReasonDescription { get; private set; } = null!;
    public Guid? InputApprovalEvidenceId { get; private set; }
    public decimal ConfirmedReceiptsAmount { get; private set; }
    public decimal CompletedRefundsAmount { get; private set; }
    public decimal ActiveRefundReservationsAmount { get; private set; }
    public decimal PinnedDepositAmount { get; private set; }
    public decimal RetainedAmount { get; private set; }
    public decimal RefundAmount { get; private set; }
    public string Currency { get; private set; } = null!;
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public Guid? PredecessorDecisionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyCancellationDecision Create(
        Guid id,
        Guid surveyOrderId,
        uint orderVersion,
        Guid requesterId,
        Guid decisionActorId,
        PinnedCancellationPolicyTerms policyTerms,
        CancellationDecisionKind decisionKind,
        string reasonCode,
        string reasonDescription,
        decimal confirmedReceiptsAmount,
        decimal completedRefundsAmount,
        decimal activeRefundReservationsAmount,
        decimal pinnedDepositAmount,
        decimal retainedAmount,
        decimal refundAmount,
        string currency,
        string payloadFingerprint,
        DateTimeOffset createdAt,
        Guid? inputApprovalEvidenceId = null,
        Guid? predecessorDecisionId = null,
        Guid? operationId = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(requesterId, nameof(requesterId));
        DomainGuard.NotEmpty(decisionActorId, nameof(decisionActorId));
        ArgumentNullException.ThrowIfNull(policyTerms);
        DomainGuard.Utc(createdAt, nameof(createdAt));

        if (!Enum.IsDefined(decisionKind))
        {
            throw new ArgumentOutOfRangeException(nameof(decisionKind));
        }

        if (predecessorDecisionId.HasValue && predecessorDecisionId.Value == Guid.Empty)
        {
            throw new ArgumentException("Predecessor decision identifier cannot be empty when provided.", nameof(predecessorDecisionId));
        }

        if (operationId.HasValue && operationId.Value == Guid.Empty)
        {
            throw new ArgumentException("Operation identifier cannot be empty when provided.", nameof(operationId));
        }

        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 64, nameof(reasonCode));
        var normalizedDescription = PolicyValidationGuard.NormalizeRequired(reasonDescription, 500, nameof(reasonDescription));
        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(payloadFingerprint, 128, nameof(payloadFingerprint));
        var validatedCurrency = PolicyValidationGuard.ValidateVndCurrency(
            CurrencyCode.Create(currency), nameof(currency));

        // Arithmetic invariants: R >= F + A
        if (confirmedReceiptsAmount < 0 || completedRefundsAmount < 0 ||
            activeRefundReservationsAmount < 0 || pinnedDepositAmount < 0 ||
            retainedAmount < 0 || refundAmount < 0)
        {
            throw new ArgumentException("Financial amounts cannot be negative.");
        }

        if (confirmedReceiptsAmount < completedRefundsAmount + activeRefundReservationsAmount)
        {
            throw new SurveyDomainException(
                CancellationDomainErrorCodes.ReceiptsLessThanRefundsAndReservations,
                $"Confirmed receipts ({confirmedReceiptsAmount}) must be greater than or equal to completed refunds ({completedRefundsAmount}) + active reservations ({activeRefundReservationsAmount}).");
        }

        var unreserved = confirmedReceiptsAmount - completedRefundsAmount - activeRefundReservationsAmount;

        var policyAllowsRefund = decisionKind switch
        {
            CancellationDecisionKind.CustomerBeforeCapture => policyTerms.RefundableBeforeCapture,
            CancellationDecisionKind.CustomerAfterAcceptableCapture => true,
            CancellationDecisionKind.OperationalPostponement => policyTerms.OperationalPostponementRefundable,
            CancellationDecisionKind.ServiceFailure => policyTerms.ServiceFailureRefundable,
            _ => false
        };

        if (!policyAllowsRefund && unreserved > 0)
        {
            throw new SurveyDomainException(
                CancellationDomainErrorCodes.PolicyDoesNotPermitRefund,
                $"Pinned cancellation policy {policyTerms.PolicyId} v{policyTerms.VersionNumber} does not permit a refund for '{decisionKind}'.");
        }

        var isAcceptableCaptureCancellation =
            decisionKind == CancellationDecisionKind.CustomerAfterAcceptableCapture;

        if (retainedAmount > 0 &&
            (!isAcceptableCaptureCancellation || !policyTerms.RetainDepositAfterCapture))
        {
            throw new SurveyDomainException(
                CancellationDomainErrorCodes.PolicyDoesNotPermitRetention,
                $"Pinned cancellation policy does not permit retention for '{decisionKind}'.");
        }

        if (isAcceptableCaptureCancellation)
        {
            if (!inputApprovalEvidenceId.HasValue || inputApprovalEvidenceId.Value == Guid.Empty)
            {
                throw new SurveyDomainException(
                    CancellationDomainErrorCodes.RetentionRequiresAcceptableCapture,
                    "Retaining deposit on cancellation requires verified acceptable capture / input approval evidence.");
            }

        }

        var maxRetention = Math.Min(pinnedDepositAmount, unreserved);
        if (retainedAmount > maxRetention)
        {
            throw new SurveyDomainException(
                CancellationDomainErrorCodes.RetentionExceedsAllowedCap,
                $"Retained amount ({retainedAmount}) cannot exceed min(D, U) = {maxRetention}.");
        }

        var expectedRefund = unreserved - retainedAmount;
        if (refundAmount != expectedRefund)
        {
            throw new SurveyDomainException(
                CancellationDomainErrorCodes.InvalidRefundAmount,
                $"Refund amount ({refundAmount}) must equal unreserved ({unreserved}) - retained ({retainedAmount}) = {expectedRefund}.");
        }

        return new SurveyCancellationDecision
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            OrderVersion = orderVersion,
            RequesterId = requesterId,
            DecisionActorId = decisionActorId,
            CancellationPolicyId = policyTerms.PolicyId,
            CancellationPolicyVersion = policyTerms.VersionNumber,
            DecisionKind = decisionKind,
            RefundableBeforeCapture = policyTerms.RefundableBeforeCapture,
            RetainDepositAfterCapture = policyTerms.RetainDepositAfterCapture,
            AllowedFreeNoShowReschedules = policyTerms.AllowedFreeNoShowReschedules,
            OperationalPostponementRefundable = policyTerms.OperationalPostponementRefundable,
            ServiceFailureRefundable = policyTerms.ServiceFailureRefundable,
            ReasonCode = normalizedReasonCode,
            ReasonDescription = normalizedDescription,
            InputApprovalEvidenceId = inputApprovalEvidenceId,
            ConfirmedReceiptsAmount = confirmedReceiptsAmount,
            CompletedRefundsAmount = completedRefundsAmount,
            ActiveRefundReservationsAmount = activeRefundReservationsAmount,
            PinnedDepositAmount = pinnedDepositAmount,
            RetainedAmount = retainedAmount,
            RefundAmount = refundAmount,
            Currency = validatedCurrency,
            OperationId = operationId ?? id,
            PayloadFingerprint = normalizedFingerprint,
            PredecessorDecisionId = predecessorDecisionId,
            CreatedAt = createdAt
        };
    }
}
