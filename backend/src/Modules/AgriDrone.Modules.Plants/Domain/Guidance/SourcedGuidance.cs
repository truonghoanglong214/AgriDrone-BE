using AgriDrone.SharedKernel.Domain;
using AgriDrone.Modules.Plants.Domain.Conditions;

namespace AgriDrone.Modules.Plants.Domain.Guidance;

public enum SourcedGuidanceStatus { Draft, Active, Retired }

/// <summary>
/// Versioned, cited operational guidance for a team-labelled suspected class.
/// It is intentionally independent of severity and does not represent a diagnosis.
/// </summary>
public sealed class SourcedGuidance : AggregateRoot
{
    private SourcedGuidance() { }
    public string Code { get; private set; } = null!;
    public int VersionNumber { get; private set; }
    public Guid? PlantConditionId { get; private set; }
    public int? PlantConditionRevisionNumber { get; private set; }
    public string SuspectedClassCode { get; private set; } = null!;
    public string SourceTitle { get; private set; } = null!;
    public string SourceReference { get; private set; } = null!;
    public string GuidanceText { get; private set; } = null!;
    public SourcedGuidanceStatus Status { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public uint Version { get; private set; }
    public PlantCondition? PlantCondition { get; private set; }

    public static SourcedGuidance CreateDraft(
        string code, int versionNumber, Guid plantConditionId,
        int plantConditionRevisionNumber, string suspectedClassCode,
        string sourceTitle, string sourceReference, string guidanceText,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo,
        Guid createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plantConditionRevisionNumber);
        DomainGuard.NotEmpty(plantConditionId); DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(effectiveFrom); DomainGuard.Utc(createdAt);
        if (effectiveTo.HasValue)
        {
            DomainGuard.Utc(effectiveTo.Value);
            if (effectiveTo <= effectiveFrom) throw new ArgumentException("Effective window is invalid.");
        }
        return new SourcedGuidance
        {
            Id = Guid.CreateVersion7(), Code = Normalize(code, 80).ToUpperInvariant(),
            VersionNumber = versionNumber,
            PlantConditionId = plantConditionId,
            PlantConditionRevisionNumber = plantConditionRevisionNumber,
            SuspectedClassCode = Normalize(suspectedClassCode, 80).ToUpperInvariant(),
            SourceTitle = Normalize(sourceTitle, 300),
            SourceReference = Normalize(sourceReference, 1000),
            GuidanceText = Normalize(guidanceText, 4000),
            Status = SourcedGuidanceStatus.Draft,
            EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo,
            CreatedBy = createdBy, CreatedAt = createdAt, UpdatedAt = createdAt
        };
    }

    public void Activate(DateTimeOffset changedAt, uint expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Sourced guidance version conflict.");
        if (Status != SourcedGuidanceStatus.Draft) throw new InvalidOperationException("Only draft guidance can be activated.");
        DomainGuard.Utc(changedAt);
        if (changedAt < UpdatedAt) throw new ArgumentException("Lifecycle time cannot move backwards.", nameof(changedAt));
        if (EffectiveTo.HasValue && EffectiveTo.Value <= changedAt)
            throw new InvalidOperationException("Expired guidance cannot be activated.");
        Status = SourcedGuidanceStatus.Active; UpdatedAt = changedAt; Version++;
    }

    public void Retire(DateTimeOffset changedAt, uint expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Sourced guidance version conflict.");
        if (Status != SourcedGuidanceStatus.Active) throw new InvalidOperationException("Only active guidance can be retired.");
        DomainGuard.Utc(changedAt);
        if (changedAt < UpdatedAt) throw new ArgumentException("Lifecycle time cannot move backwards.", nameof(changedAt));
        Status = SourcedGuidanceStatus.Retired;
        RetiredAt = changedAt;
        EffectiveTo ??= changedAt;
        UpdatedAt = changedAt;
        Version++;
    }

    public bool IsEffectiveAt(DateTimeOffset evaluatedAt) =>
        Status == SourcedGuidanceStatus.Active &&
        EffectiveFrom <= evaluatedAt &&
        (!EffectiveTo.HasValue || evaluatedAt < EffectiveTo.Value);

    private static string Normalize(string value, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            normalized.Length,
            maximumLength,
            nameof(value));
        return normalized;
    }
}
