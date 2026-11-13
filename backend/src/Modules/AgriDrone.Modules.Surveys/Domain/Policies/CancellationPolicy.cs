using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CancellationPolicy : Entity
{
    private CancellationPolicy() { }

    public int VersionNumber { get; private set; }
    public Guid? PredecessorVersionId { get; private set; }
    public bool RefundableBeforeCapture { get; private set; }
    public bool RetainDepositAfterCapture { get; private set; }
    public int AllowedFreeNoShowReschedules { get; private set; }
    public bool OperationalPostponementRefundable { get; private set; }
    public bool ServiceFailureRefundable { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static CancellationPolicy Create(
        Guid id,
        int versionNumber,
        Guid? predecessorVersionId,
        bool refundableBeforeCapture,
        bool retainDepositAfterCapture,
        int allowedFreeNoShowReschedules,
        bool operationalPostponementRefundable,
        bool serviceFailureRefundable,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid createdBy,
        DateTimeOffset createdAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(createdBy, nameof(createdBy));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        PolicyValidationGuard.ValidateVersionAndPredecessor(id, versionNumber, predecessorVersionId);
        PolicyValidationGuard.ValidateEffectiveWindow(effectiveFrom, effectiveTo);

        if (allowedFreeNoShowReschedules < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allowedFreeNoShowReschedules),
                allowedFreeNoShowReschedules,
                "Allowed free no-show reschedules cannot be negative.");
        }

        var normalizedReasonCode = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        return new CancellationPolicy
        {
            Id = id,
            VersionNumber = versionNumber,
            PredecessorVersionId = predecessorVersionId,
            RefundableBeforeCapture = refundableBeforeCapture,
            RetainDepositAfterCapture = retainDepositAfterCapture,
            AllowedFreeNoShowReschedules = allowedFreeNoShowReschedules,
            OperationalPostponementRefundable = operationalPostponementRefundable,
            ServiceFailureRefundable = serviceFailureRefundable,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ReasonCode = normalizedReasonCode,
            EvidenceReference = normalizedEvidence
        };
    }

    public bool IsEffectiveAt(DateTimeOffset evaluatedAt)
    {
        DomainGuard.Utc(evaluatedAt, nameof(evaluatedAt));
        return EffectiveFrom <= evaluatedAt &&
               (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        DomainGuard.Utc(effectiveTo, nameof(effectiveTo));
        if (EffectiveTo.HasValue)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.WindowAlreadyClosed,
                "A closed cancellation policy effective window cannot be changed.");
        }

        if (effectiveTo <= EffectiveFrom)
        {
            throw new SurveyDomainException(
                PolicyDomainErrorCodes.InvalidEffectiveWindow,
                "Cancellation policy effective end must be after its start.");
        }

        EffectiveTo = effectiveTo;
    }
}
