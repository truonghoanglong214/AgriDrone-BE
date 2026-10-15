using AgriDrone.SharedKernel.Domain;

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
    public string SuspectedClassCode { get; private set; } = null!;
    public string SourceTitle { get; private set; } = null!;
    public string SourceReference { get; private set; } = null!;
    public string GuidanceText { get; private set; } = null!;
    public SourcedGuidanceStatus Status { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; }

    public static SourcedGuidance CreateDraft(
        string code, int versionNumber, string suspectedClassCode,
        string sourceTitle, string sourceReference, string guidanceText,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo,
        Guid createdBy, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        DomainGuard.NotEmpty(createdBy); DomainGuard.Utc(effectiveFrom); DomainGuard.Utc(createdAt);
        if (effectiveTo.HasValue)
        {
            DomainGuard.Utc(effectiveTo.Value);
            if (effectiveTo <= effectiveFrom) throw new ArgumentException("Effective window is invalid.");
        }
        return new SourcedGuidance
        {
            Id = Guid.CreateVersion7(), Code = Normalize(code, 80).ToUpperInvariant(),
            VersionNumber = versionNumber,
            SuspectedClassCode = Normalize(suspectedClassCode, 80).ToUpperInvariant(),
            SourceTitle = Normalize(sourceTitle, 300),
            SourceReference = Normalize(sourceReference, 1000),
            GuidanceText = Normalize(guidanceText, 4000),
            Status = SourcedGuidanceStatus.Draft,
            EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo,
            CreatedBy = createdBy, CreatedAt = createdAt
        };
    }

    public void Activate(DateTimeOffset changedAt, uint expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Sourced guidance version conflict.");
        if (Status != SourcedGuidanceStatus.Draft) throw new InvalidOperationException("Only draft guidance can be activated.");
        DomainGuard.Utc(changedAt); Status = SourcedGuidanceStatus.Active; Version++;
    }

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
