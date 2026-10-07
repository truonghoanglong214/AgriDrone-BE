using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Domain.Boundaries;

public sealed class FarmBoundary : AggregateRoot
{
    private FarmBoundary() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public int VersionNumber { get; private set; }
    public Polygon Geometry { get; private set; } = null!;
    public FarmBoundarySource Source { get; private set; }
    public Guid? SourceSurveyRequestId { get; private set; }
    public Guid? SubmittedBy { get; private set; }
    public FarmBoundaryStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewReason { get; private set; }
    public Guid? SupersededByBoundaryId { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static FarmBoundary CreateDraft(
        Guid tenantId,
        Guid farmId,
        int versionNumber,
        Polygon geometry,
        FarmBoundarySource source,
        Guid? sourceSurveyRequestId,
        Guid? submittedBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versionNumber);
        DomainGuard.Utc(createdAt);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (sourceSurveyRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "Source survey request identifier cannot be empty.",
                nameof(sourceSurveyRequestId));
        }

        if (submittedBy == Guid.Empty)
        {
            throw new ArgumentException(
                "Submitter identifier cannot be empty.",
                nameof(submittedBy));
        }

        if (source == FarmBoundarySource.LegacyImport &&
            sourceSurveyRequestId.HasValue)
        {
            throw new ArgumentException(
                "A legacy-import boundary cannot reference a survey request.",
                nameof(sourceSurveyRequestId));
        }

        if (source != FarmBoundarySource.LegacyImport &&
            sourceSurveyRequestId is null &&
            submittedBy is null)
        {
            throw new ArgumentException(
                "A submitted boundary must identify its survey request or submitter.",
                nameof(submittedBy));
        }

        return new FarmBoundary
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            VersionNumber = versionNumber,
            Geometry = FarmBoundaryGeometryGuard.ValidPolygon(
                geometry,
                nameof(geometry)),
            Source = source,
            SourceSurveyRequestId = sourceSurveyRequestId,
            SubmittedBy = submittedBy,
            Status = FarmBoundaryStatus.Draft,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void Reject(
        Guid reviewedBy,
        string reason,
        DateTimeOffset reviewedAt)
    {
        EnsureDraft();
        ValidateReview(reviewedBy, reason, reviewedAt);

        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = reason.Trim();
        Status = FarmBoundaryStatus.Rejected;
        UpdatedAt = reviewedAt;
        Version++;
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.VersionConflict,
                $"Expected FarmBoundary version {expectedVersion}, but found {Version}.");
        }
    }

    internal void Approve(
        Guid reviewedBy,
        string reason,
        DateTimeOffset reviewedAt)
    {
        EnsureDraft();
        ValidateReview(reviewedBy, reason, reviewedAt);

        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
        ReviewReason = reason.Trim();
        Status = FarmBoundaryStatus.Approved;
        UpdatedAt = reviewedAt;
        Version++;
    }

    internal void Supersede(
        Guid replacementBoundaryId,
        DateTimeOffset supersededAt)
    {
        EnsureCanSupersede(replacementBoundaryId, supersededAt);

        SupersededByBoundaryId = replacementBoundaryId;
        SupersededAt = supersededAt;
        Status = FarmBoundaryStatus.Superseded;
        UpdatedAt = supersededAt;
        Version++;
    }

    internal void EnsureCanApprove(
        Guid reviewedBy,
        string reason,
        DateTimeOffset reviewedAt)
    {
        EnsureDraft();
        ValidateReview(reviewedBy, reason, reviewedAt);
    }

    internal void EnsureCanSupersede(
        Guid replacementBoundaryId,
        DateTimeOffset supersededAt)
    {
        DomainGuard.NotEmpty(replacementBoundaryId);
        DomainGuard.Utc(supersededAt);

        if (Status != FarmBoundaryStatus.Approved)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.InvalidTransition,
                "Only an approved FarmBoundary can be superseded.");
        }

        if (replacementBoundaryId == Id)
        {
            throw new ArgumentException(
                "A FarmBoundary cannot supersede itself.",
                nameof(replacementBoundaryId));
        }

        if (supersededAt < UpdatedAt)
        {
            throw new ArgumentException(
                "Superseded time cannot be earlier than the last update.",
                nameof(supersededAt));
        }
    }

    private void EnsureDraft()
    {
        if (Status != FarmBoundaryStatus.Draft)
        {
            throw new FarmBoundaryDomainException(
                FarmBoundaryDomainErrorCodes.InvalidTransition,
                "Only a draft FarmBoundary can be reviewed.");
        }
    }

    private void ValidateReview(
        Guid reviewedBy,
        string reason,
        DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewedBy);
        DomainGuard.Utc(reviewedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (reason.Trim().Length > 2000)
        {
            throw new ArgumentException(
                "Review reason cannot exceed 2000 characters.",
                nameof(reason));
        }

        if (reviewedAt < CreatedAt)
        {
            throw new ArgumentException(
                "Review time cannot be earlier than creation time.",
                nameof(reviewedAt));
        }
    }
}
