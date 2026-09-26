using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyResult : AggregateRoot
{
    private static readonly Dictionary<SurveyResultStatus, IReadOnlySet<SurveyResultStatus>>
        AllowedTransitions = new Dictionary<SurveyResultStatus, IReadOnlySet<SurveyResultStatus>>
        {
            [SurveyResultStatus.PendingReview] = new HashSet<SurveyResultStatus>
            {
                SurveyResultStatus.Approved
            },
            [SurveyResultStatus.Approved] = new HashSet<SurveyResultStatus>
            {
                SurveyResultStatus.Published
            }
        };

    private SurveyResult() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public SurveyServiceType ServiceType { get; private set; }
    public SurveyResultStatus Status { get; private set; }
    public JsonDocument Provenance { get; private set; } = null!;
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;
    public ICollection<HarvestReadinessAssessment> HarvestReadinessAssessments { get; private set; } = [];

    public static bool CanTransition(SurveyResultStatus from, SurveyResultStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static SurveyResult CreateForManagerReview(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        SurveyServiceType serviceType,
        JsonDocument provenance,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.Utc(createdAt);
        ArgumentNullException.ThrowIfNull(provenance);

        return new SurveyResult
        {
            Id = Guid.NewGuid(),
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            FarmId = farmId,
            ServiceType = serviceType,
            Status = SurveyResultStatus.PendingReview,
            Provenance = JsonDocument.Parse(provenance.RootElement.GetRawText()),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                SurveyResultDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void Approve(Guid reviewedBy, DateTimeOffset reviewedAt)
    {
        DomainGuard.NotEmpty(reviewedBy);
        TransitionTo(SurveyResultStatus.Approved, reviewedAt);
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }

    public void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(publishedBy);
        TransitionTo(SurveyResultStatus.Published, publishedAt);
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
    }

    private void TransitionTo(SurveyResultStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            SurveyResultDomainErrorCodes.InvalidTransition);
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }
}
