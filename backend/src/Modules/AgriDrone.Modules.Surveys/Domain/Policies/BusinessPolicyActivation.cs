using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class BusinessPolicyActivation : AggregateRoot
{
    private BusinessPolicyActivation() { }

    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid ActiveBusinessPolicyVersionId { get; private set; }
    public bool PublicIntakeEnabled { get; private set; }
    public bool CaptureEnabled { get; private set; }
    public bool ReleaseEnabled { get; private set; }
    public Guid LastDecisionId { get; private set; }
    public DateTimeOffset ActivatedAt { get; private set; }
    public Guid ActivatedBy { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    public static (BusinessPolicyActivation Activation, BusinessPolicyActivationDecision InitialDecision) CreateInitial(
        Guid activeBusinessPolicyVersionId,
        Guid initialDecisionId,
        Guid activatedBy,
        DateTimeOffset activatedAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(activeBusinessPolicyVersionId, nameof(activeBusinessPolicyVersionId));
        DomainGuard.NotEmpty(initialDecisionId, nameof(initialDecisionId));
        DomainGuard.NotEmpty(activatedBy, nameof(activatedBy));
        DomainGuard.Utc(activatedAt, nameof(activatedAt));

        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        var initialDecision = BusinessPolicyActivationDecision.Create(
            initialDecisionId,
            SingletonId,
            activeBusinessPolicyVersionId,
            publicIntakeEnabled: false,
            captureEnabled: false,
            releaseEnabled: false,
            predecessorDecisionId: null,
            activatedBy,
            activatedAt,
            normalizedReason,
            normalizedEvidence);

        var activation = new BusinessPolicyActivation
        {
            Id = SingletonId,
            ActiveBusinessPolicyVersionId = activeBusinessPolicyVersionId,
            PublicIntakeEnabled = false,
            CaptureEnabled = false,
            ReleaseEnabled = false,
            LastDecisionId = initialDecisionId,
            ActivatedAt = activatedAt,
            ActivatedBy = activatedBy,
            ReasonCode = normalizedReason,
            EvidenceReference = normalizedEvidence
        };

        return (activation, initialDecision);
    }

    public BusinessPolicyActivationDecision ApplyDecision(
        Guid decisionId,
        Guid activeBusinessPolicyVersionId,
        bool publicIntakeEnabled,
        bool captureEnabled,
        bool releaseEnabled,
        Guid changedBy,
        DateTimeOffset changedAt,
        string reasonCode,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(decisionId, nameof(decisionId));
        DomainGuard.NotEmpty(activeBusinessPolicyVersionId, nameof(activeBusinessPolicyVersionId));
        DomainGuard.NotEmpty(changedBy, nameof(changedBy));
        DomainGuard.Utc(changedAt, nameof(changedAt));

        if (changedAt <= ActivatedAt)
        {
            throw new ArgumentException(
                "Activation transition timestamp must be later than current activation timestamp.",
                nameof(changedAt));
        }

        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reasonCode, 100, nameof(reasonCode));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        var decision = BusinessPolicyActivationDecision.Create(
            decisionId,
            Id,
            activeBusinessPolicyVersionId,
            publicIntakeEnabled,
            captureEnabled,
            releaseEnabled,
            LastDecisionId,
            changedBy,
            changedAt,
            normalizedReason,
            normalizedEvidence);

        ActiveBusinessPolicyVersionId = activeBusinessPolicyVersionId;
        PublicIntakeEnabled = publicIntakeEnabled;
        CaptureEnabled = captureEnabled;
        ReleaseEnabled = releaseEnabled;
        LastDecisionId = decision.Id;
        ActivatedAt = changedAt;
        ActivatedBy = changedBy;
        ReasonCode = normalizedReason;
        EvidenceReference = normalizedEvidence;

        return decision;
    }
}
