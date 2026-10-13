using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class PriceAdjustment : Entity
{
    private static readonly Dictionary<PriceAdjustmentStatus, IReadOnlySet<PriceAdjustmentStatus>>
        AllowedTransitions = new()
        {
            [PriceAdjustmentStatus.Pending] = new HashSet<PriceAdjustmentStatus>
            {
                PriceAdjustmentStatus.Approved,
                PriceAdjustmentStatus.Rejected
            },
            [PriceAdjustmentStatus.Approved] = new HashSet<PriceAdjustmentStatus>
            {
                PriceAdjustmentStatus.Applied
            }
        };

    private PriceAdjustment() { }

    public Guid SurveyOrderId { get; private set; }
    public int OldPoleCount { get; private set; }
    public int NewPoleCount { get; private set; }
    public Guid OldSurveyServicePriceId { get; private set; }
    public Guid NewSurveyServicePriceId { get; private set; }
    public decimal OldPricePerPole { get; private set; }
    public decimal NewPricePerPole { get; private set; }
    public decimal OldFinalPrice { get; private set; }
    public decimal NewFinalPrice { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public string EvidenceReference { get; private set; } = null!;
    public PriceAdjustmentStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public string? ReviewEvidenceReference { get; private set; }
    public Guid? AppliedBy { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static PriceAdjustment Create(
        Guid surveyOrderId,
        ConfirmedSurveyPoleCount oldPoleCount,
        ConfirmedSurveyPoleCount newPoleCount,
        Guid oldSurveyServicePriceId,
        Guid newSurveyServicePriceId,
        PricePerPole oldPricePerPole,
        PricePerPole newPricePerPole,
        CurrencyCode currency,
        string reason,
        string evidenceReference,
        Guid requestedBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(oldSurveyServicePriceId);
        DomainGuard.NotEmpty(newSurveyServicePriceId);
        DomainGuard.NotEmpty(requestedBy);
        DomainGuard.Utc(createdAt);
        var normalizedReason = Normalize(reason, nameof(reason), 2000);
        var normalizedEvidence = Normalize(
            evidenceReference,
            nameof(evidenceReference),
            1000);
        var oldFinalPrice = Money.CalculateSurveyPrice(
            oldPoleCount,
            oldPricePerPole,
            currency);
        var newFinalPrice = Money.CalculateSurveyPrice(
            newPoleCount,
            newPricePerPole,
            currency);

        if (oldPoleCount == newPoleCount &&
            oldSurveyServicePriceId == newSurveyServicePriceId &&
            oldPricePerPole == newPricePerPole)
        {
            throw new SurveyDomainException(
                PriceAdjustmentDomainErrorCodes.NoChange,
                "A price adjustment must change the pole count or price snapshot.");
        }

        return new PriceAdjustment
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = surveyOrderId,
            OldPoleCount = oldPoleCount.Value,
            NewPoleCount = newPoleCount.Value,
            OldSurveyServicePriceId = oldSurveyServicePriceId,
            NewSurveyServicePriceId = newSurveyServicePriceId,
            OldPricePerPole = oldPricePerPole.Amount,
            NewPricePerPole = newPricePerPole.Amount,
            OldFinalPrice = oldFinalPrice.Amount,
            NewFinalPrice = newFinalPrice.Amount,
            Currency = currency.Value,
            Reason = normalizedReason,
            EvidenceReference = normalizedEvidence,
            Status = PriceAdjustmentStatus.Pending,
            RequestedBy = requestedBy,
            CreatedAt = createdAt
        };
    }

    public static bool CanTransition(
        PriceAdjustmentStatus from,
        PriceAdjustmentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                PriceAdjustmentDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void Approve(
        Guid approvedBy,
        string reason,
        string evidenceReference,
        DateTimeOffset approvedAt) =>
        Review(
            PriceAdjustmentStatus.Approved,
            approvedBy,
            reason,
            evidenceReference,
            approvedAt);

    // Compatibility for the phase-0 state-machine callers. Runtime 4E commands
    // always use the evidence-bearing overload above.
    public void Approve(Guid approvedBy, DateTimeOffset approvedAt) =>
        Approve(
            approvedBy,
            "Legacy approval",
            "LEGACY_STATE_MACHINE",
            approvedAt);

    public void Reject(
        Guid rejectedBy,
        string reason,
        string evidenceReference,
        DateTimeOffset rejectedAt) =>
        Review(
            PriceAdjustmentStatus.Rejected,
            rejectedBy,
            reason,
            evidenceReference,
            rejectedAt);

    public void MarkApplied(Guid appliedBy, DateTimeOffset appliedAt)
    {
        DomainGuard.NotEmpty(appliedBy);
        DomainGuard.Utc(appliedAt);
        EnsureTransition(PriceAdjustmentStatus.Applied);
        Status = PriceAdjustmentStatus.Applied;
        AppliedBy = appliedBy;
        AppliedAt = appliedAt;
        Version++;
    }

    // Compatibility for the phase-0 state-machine callers.
    public void MarkApplied(DateTimeOffset appliedAt)
    {
        EnsureTransition(PriceAdjustmentStatus.Applied);
        if (ReviewedBy is not Guid reviewedBy)
        {
            throw new SurveyDomainException(
                PriceAdjustmentDomainErrorCodes.InvalidTransition,
                "An approved adjustment must have a reviewer before application.");
        }

        MarkApplied(reviewedBy, appliedAt);
    }

    private void Review(
        PriceAdjustmentStatus target,
        Guid reviewedBy,
        string reason,
        string evidenceReference,
        DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewedBy);
        DomainGuard.Utc(reviewedAt);
        EnsureTransition(target);
        Status = target;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = Normalize(reason, nameof(reason), 2000);
        ReviewEvidenceReference = Normalize(
            evidenceReference,
            nameof(evidenceReference),
            1000);
        Version++;
    }

    private void EnsureTransition(PriceAdjustmentStatus target) =>
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            PriceAdjustmentDomainErrorCodes.InvalidTransition);

    private static string Normalize(string value, string name, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                name,
                $"{name} cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}
