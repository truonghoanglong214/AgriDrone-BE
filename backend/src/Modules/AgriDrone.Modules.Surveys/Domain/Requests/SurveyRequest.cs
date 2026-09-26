using System.Text.Json;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyRequest : AggregateRoot
{
    private static readonly Dictionary<SurveyRequestStatus, IReadOnlySet<SurveyRequestStatus>>
        AllowedTransitions = new Dictionary<SurveyRequestStatus, IReadOnlySet<SurveyRequestStatus>>
        {
            [SurveyRequestStatus.Submitted] = new HashSet<SurveyRequestStatus>
            {
                SurveyRequestStatus.UnderReview,
                SurveyRequestStatus.Withdrawn
            },
            [SurveyRequestStatus.UnderReview] = new HashSet<SurveyRequestStatus>
            {
                SurveyRequestStatus.Approved,
                SurveyRequestStatus.Rejected,
                SurveyRequestStatus.Withdrawn
            }
        };

    private SurveyRequest() { }

    public string RequestNumber { get; private set; } = null!;
    public SurveyRequestKind Kind { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? FarmId { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public Guid SurveyServiceId { get; private set; }
    public string CallerScope { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public string ApplicantName { get; private set; } = null!;
    public string ApplicantEmail { get; private set; } = null!;
    public string ApplicantPhone { get; private set; } = null!;
    public string FarmName { get; private set; } = null!;
    public string FarmAddress { get; private set; } = null!;
    public decimal ApproximateAreaHa { get; private set; }
    public Point MapLocation { get; private set; } = null!;
    public int? EstimatedPoleCount { get; private set; }
    public DateTimeOffset? PreferredStartAt { get; private set; }
    public DateTimeOffset? PreferredEndAt { get; private set; }
    public string? Notes { get; private set; }
    public SurveyRequestStatus Status { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyService SurveyService { get; private set; } = null!;
    public ICollection<SurveyRequestReview> Reviews { get; private set; } = [];

    public static bool CanTransition(
        SurveyRequestStatus from,
        SurveyRequestStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                SurveyRequestDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void StartReview(DateTimeOffset reviewedAt) =>
        TransitionTo(SurveyRequestStatus.UnderReview, reviewedAt);

    public void Approve(
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt) =>
        RecordDecision(
            SurveyReviewDecision.Approved,
            SurveyRequestStatus.Approved,
            checklistSnapshot,
            reason,
            reviewedBy,
            reviewedAt);

    public void Reject(
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt) =>
        RecordDecision(
            SurveyReviewDecision.Rejected,
            SurveyRequestStatus.Rejected,
            checklistSnapshot,
            reason,
            reviewedBy,
            reviewedAt);

    public void Withdraw(DateTimeOffset withdrawnAt) =>
        TransitionTo(SurveyRequestStatus.Withdrawn, withdrawnAt);

    private void RecordDecision(
        SurveyReviewDecision decision,
        SurveyRequestStatus target,
        JsonDocument checklistSnapshot,
        string reason,
        Guid reviewedBy,
        DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(checklistSnapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        DomainGuard.NotEmpty(reviewedBy);
        SurveyTransitionGuard.EnsureTimestamp(reviewedAt, UpdatedAt);
        EnsureTransition(target);

        Reviews.Add(SurveyRequestReview.Create(
            Id,
            decision,
            checklistSnapshot,
            reason.Trim(),
            reviewedBy,
            reviewedAt));
        SetState(target, reviewedAt);
    }

    private void TransitionTo(SurveyRequestStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        EnsureTransition(target);
        SetState(target, occurredAt);
    }

    private void EnsureTransition(SurveyRequestStatus target) =>
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            SurveyRequestDomainErrorCodes.InvalidTransition);

    private void SetState(SurveyRequestStatus target, DateTimeOffset occurredAt)
    {
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }
}
