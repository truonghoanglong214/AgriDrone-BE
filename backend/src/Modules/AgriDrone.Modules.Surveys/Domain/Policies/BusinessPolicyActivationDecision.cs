using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class BusinessPolicyActivationDecision : Entity
{
    private BusinessPolicyActivationDecision() { }

    public Guid ActivationId { get; private set; }
    public Guid BusinessPolicyVersionId { get; private set; }
    public bool PublicIntakeEnabled { get; private set; }
    public bool CaptureEnabled { get; private set; }
    public bool ReleaseEnabled { get; private set; }
    public Guid? PredecessorDecisionId { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string ReasonCode { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }

    internal static BusinessPolicyActivationDecision Create(
        Guid id,
        Guid activationId,
        Guid businessPolicyVersionId,
        bool publicIntakeEnabled,
        bool captureEnabled,
        bool releaseEnabled,
        Guid? predecessorDecisionId,
        Guid changedBy,
        DateTimeOffset changedAt,
        string reasonCode,
        string? evidenceReference)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(activationId, nameof(activationId));
        DomainGuard.NotEmpty(businessPolicyVersionId, nameof(businessPolicyVersionId));
        DomainGuard.NotEmpty(changedBy, nameof(changedBy));
        DomainGuard.Utc(changedAt, nameof(changedAt));

        if (predecessorDecisionId.HasValue && predecessorDecisionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Predecessor decision identifier cannot be empty.",
                nameof(predecessorDecisionId));
        }

        if (predecessorDecisionId.HasValue && predecessorDecisionId.Value == id)
        {
            throw new ArgumentException(
                "A decision cannot reference itself as its predecessor.",
                nameof(predecessorDecisionId));
        }

        return new BusinessPolicyActivationDecision
        {
            Id = id,
            ActivationId = activationId,
            BusinessPolicyVersionId = businessPolicyVersionId,
            PublicIntakeEnabled = publicIntakeEnabled,
            CaptureEnabled = captureEnabled,
            ReleaseEnabled = releaseEnabled,
            PredecessorDecisionId = predecessorDecisionId,
            ChangedBy = changedBy,
            ChangedAt = changedAt,
            ReasonCode = reasonCode,
            EvidenceReference = evidenceReference
        };
    }
}
