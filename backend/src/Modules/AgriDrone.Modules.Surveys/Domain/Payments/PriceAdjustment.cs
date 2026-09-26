using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class PriceAdjustment : Entity
{
    private static readonly Dictionary<PriceAdjustmentStatus, IReadOnlySet<PriceAdjustmentStatus>>
        AllowedTransitions = new Dictionary<PriceAdjustmentStatus, IReadOnlySet<PriceAdjustmentStatus>>
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
    public decimal OldAreaHa { get; private set; }
    public decimal NewAreaHa { get; private set; }
    public decimal OldPrice { get; private set; }
    public decimal NewPrice { get; private set; }
    public string Reason { get; private set; } = null!;
    public PriceAdjustmentStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;

    public static bool CanTransition(
        PriceAdjustmentStatus from,
        PriceAdjustmentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void Approve(Guid approvedBy, DateTimeOffset approvedAt)
    {
        DomainGuard.NotEmpty(approvedBy);
        DomainGuard.Utc(approvedAt);
        EnsureTransition(PriceAdjustmentStatus.Approved);
        Status = PriceAdjustmentStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
    }

    public void Reject(DateTimeOffset rejectedAt)
    {
        DomainGuard.Utc(rejectedAt);
        EnsureTransition(PriceAdjustmentStatus.Rejected);
        Status = PriceAdjustmentStatus.Rejected;
    }

    public void MarkApplied(DateTimeOffset appliedAt)
    {
        DomainGuard.Utc(appliedAt);
        EnsureTransition(PriceAdjustmentStatus.Applied);
        Status = PriceAdjustmentStatus.Applied;
    }

    private void EnsureTransition(PriceAdjustmentStatus target) =>
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            PriceAdjustmentDomainErrorCodes.InvalidTransition);
}
