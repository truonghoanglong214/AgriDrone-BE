using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Recommendations;

public sealed class TreatmentRecommendation : AggregateRoot
{
    private TreatmentRecommendation() { }

    public string Code { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public Guid PlantConditionId { get; private set; }
    public Guid HealthLevelId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Guidance { get; private set; } = null!;
    public string AdvisoryDisclaimer { get; private set; } = null!;
    public string ExpertSource { get; private set; } = null!;
    public string SourceReference { get; private set; } = null!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid? SupersedesRecommendationId { get; private set; }
    public TreatmentRecommendationStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? RetiredBy { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public Guid? SupersededByRecommendationId { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static TreatmentRecommendation CreateDraft(
        string code,
        int versionNumber,
        Guid plantConditionId,
        Guid healthLevelId,
        string title,
        string guidance,
        string advisoryDisclaimer,
        string expertSource,
        string sourceReference,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? supersedesRecommendationId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (code.Trim().Length > 80)
        {
            throw new ArgumentException("Recommendation code cannot exceed 80 characters.", nameof(code));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        DomainGuard.NotEmpty(plantConditionId);
        DomainGuard.NotEmpty(healthLevelId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom);
        DomainGuard.Utc(createdAt);
        ValidateText(title, 200, nameof(title));
        ValidateText(guidance, 4000, nameof(guidance));
        ValidateText(advisoryDisclaimer, 2000, nameof(advisoryDisclaimer));
        ValidateText(expertSource, 300, nameof(expertSource));
        ValidateText(sourceReference, 1000, nameof(sourceReference));

        if (effectiveTo.HasValue)
        {
            DomainGuard.Utc(effectiveTo.Value);
            if (effectiveTo <= effectiveFrom)
            {
                throw new ArgumentException("EffectiveTo must be later than EffectiveFrom.", nameof(effectiveTo));
            }
        }

        if (supersedesRecommendationId == Guid.Empty)
        {
            throw new ArgumentException("Superseded recommendation identifier cannot be empty.", nameof(supersedesRecommendationId));
        }

        if (versionNumber == 1 && supersedesRecommendationId.HasValue)
        {
            throw new ArgumentException("The first recommendation version cannot supersede another version.", nameof(supersedesRecommendationId));
        }

        if (versionNumber > 1 && supersedesRecommendationId is null)
        {
            throw new ArgumentException("A later recommendation version must identify the version it supersedes.", nameof(supersedesRecommendationId));
        }

        return new TreatmentRecommendation
        {
            Id = Guid.NewGuid(),
            Code = code.Trim().ToUpperInvariant(),
            VersionNumber = versionNumber,
            PlantConditionId = plantConditionId,
            HealthLevelId = healthLevelId,
            Title = title.Trim(),
            Guidance = guidance.Trim(),
            AdvisoryDisclaimer = advisoryDisclaimer.Trim(),
            ExpertSource = expertSource.Trim(),
            SourceReference = sourceReference.Trim(),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SupersedesRecommendationId = supersedesRecommendationId,
            Status = TreatmentRecommendationStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        EnsureStatus(TreatmentRecommendationStatus.Draft);
        DomainGuard.NotEmpty(publishedBy);
        EnsureTimestamp(publishedAt);

        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
        Status = TreatmentRecommendationStatus.Published;
        UpdatedAt = publishedAt;
        Version++;
    }

    public void Retire(Guid retiredBy, DateTimeOffset retiredAt)
    {
        EnsureStatus(TreatmentRecommendationStatus.Published);
        DomainGuard.NotEmpty(retiredBy);
        EnsureTimestamp(retiredAt);

        RetiredBy = retiredBy;
        RetiredAt = retiredAt;
        Status = TreatmentRecommendationStatus.Retired;
        UpdatedAt = retiredAt;
        Version++;
    }

    public bool IsApplicable(Guid plantConditionId, Guid healthLevelId, DateTimeOffset at)
    {
        DomainGuard.Utc(at);
        return Status == TreatmentRecommendationStatus.Published &&
               PlantConditionId == plantConditionId &&
               HealthLevelId == healthLevelId &&
               EffectiveFrom <= at &&
               (!EffectiveTo.HasValue || at < EffectiveTo.Value);
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new TreatmentRecommendationDomainException(
                TreatmentRecommendationDomainErrorCodes.VersionConflict,
                $"Expected TreatmentRecommendation version {expectedVersion}, but found {Version}.");
        }
    }

    internal void EnsureCanSupersede(
        TreatmentRecommendation replacement,
        DateTimeOffset supersededAt)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        EnsureStatus(TreatmentRecommendationStatus.Published);
        EnsureTimestamp(supersededAt);

        if (replacement.Status != TreatmentRecommendationStatus.Draft ||
            replacement.Code != Code ||
            replacement.VersionNumber <= VersionNumber ||
            replacement.SupersedesRecommendationId != Id ||
            replacement.PlantConditionId != PlantConditionId ||
            replacement.HealthLevelId != HealthLevelId)
        {
            throw new TreatmentRecommendationDomainException(
                TreatmentRecommendationDomainErrorCodes.ReplacementMismatch,
                "Replacement must be a newer draft for the same code, condition and severity.");
        }
    }

    internal void Supersede(Guid replacementId, DateTimeOffset supersededAt)
    {
        SupersededByRecommendationId = replacementId;
        SupersededAt = supersededAt;
        Status = TreatmentRecommendationStatus.Superseded;
        UpdatedAt = supersededAt;
        Version++;
    }

    private void EnsureStatus(TreatmentRecommendationStatus expected)
    {
        if (Status != expected)
        {
            throw new TreatmentRecommendationDomainException(
                TreatmentRecommendationDomainErrorCodes.InvalidTransition,
                $"TreatmentRecommendation must be {expected} for this operation.");
        }
    }

    private void EnsureTimestamp(DateTimeOffset occurredAt)
    {
        DomainGuard.Utc(occurredAt);
        if (occurredAt < UpdatedAt)
        {
            throw new ArgumentException("Lifecycle time cannot be earlier than the last update.", nameof(occurredAt));
        }
    }

    private static void ValidateText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        }
    }
}
