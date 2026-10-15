using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum ScopeAdjustmentStatus { Pending, Approved, Rejected, Applied }
public enum ScopeAdjustmentReason { ConfirmedPoleCountChanged, ApprovedBoundaryChanged }

public sealed class ScopeAdjustment : Entity
{
    private ScopeAdjustment() { }

    public Guid SurveyOrderId { get; private set; }
    public ScopeAdjustmentReason Reason { get; private set; }
    public Guid? BeforeBoundaryVersionId { get; private set; }
    public Guid? AfterBoundaryVersionId { get; private set; }
    public int BeforePoleCount { get; private set; }
    public int AfterPoleCount { get; private set; }
    public decimal FinancialImpact { get; private set; }
    public string Currency { get; private set; } = null!;
    public string EvidenceReference { get; private set; } = null!;
    public ScopeAdjustmentStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static ScopeAdjustment Create(
        Guid surveyOrderId,
        ScopeAdjustmentReason reason,
        Guid? beforeBoundaryVersionId,
        Guid? afterBoundaryVersionId,
        ConfirmedSurveyPoleCount beforePoleCount,
        ConfirmedSurveyPoleCount afterPoleCount,
        decimal financialImpact,
        CurrencyCode currency,
        string evidenceReference,
        Guid requestedBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(requestedBy);
        DomainGuard.Utc(createdAt);
        if (beforePoleCount == afterPoleCount &&
            beforeBoundaryVersionId == afterBoundaryVersionId)
        {
            throw new ArgumentException("A scope adjustment must change scope or pole count.");
        }

        return new ScopeAdjustment
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = surveyOrderId,
            Reason = reason,
            BeforeBoundaryVersionId = beforeBoundaryVersionId,
            AfterBoundaryVersionId = afterBoundaryVersionId,
            BeforePoleCount = beforePoleCount.Value,
            AfterPoleCount = afterPoleCount.Value,
            FinancialImpact = financialImpact,
            Currency = currency.Value,
            EvidenceReference = Normalize(evidenceReference, 1000),
            Status = ScopeAdjustmentStatus.Pending,
            RequestedBy = requestedBy,
            CreatedAt = createdAt
        };
    }

    public void Review(
        bool approved,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt,
        uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        if (Status != ScopeAdjustmentStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending scope adjustment can be reviewed.");
        }

        DomainGuard.NotEmpty(reviewedBy);
        DomainGuard.Utc(reviewedAt);
        Status = approved ? ScopeAdjustmentStatus.Approved : ScopeAdjustmentStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = Normalize(reason, 2000);
        Version++;
    }

    public void MarkApplied(DateTimeOffset appliedAt, uint expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.Utc(appliedAt);
        if (Status != ScopeAdjustmentStatus.Approved)
        {
            throw new InvalidOperationException("Only an approved scope adjustment can be applied.");
        }

        Status = ScopeAdjustmentStatus.Applied;
        Version++;
    }

    private void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Scope adjustment version conflict.");
    }

    private static string Normalize(string value, int max)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            normalized.Length,
            max,
            nameof(value));
        return normalized;
    }
}
