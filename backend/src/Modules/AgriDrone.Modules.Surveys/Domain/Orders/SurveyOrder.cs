using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrder : AggregateRoot
{
    private static readonly Dictionary<SurveyOrderStatus, IReadOnlySet<SurveyOrderStatus>>
        AllowedTransitions = new Dictionary<SurveyOrderStatus, IReadOnlySet<SurveyOrderStatus>>
        {
            [SurveyOrderStatus.PendingScopeConfirmation] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingAppointment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingAppointment] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingPayment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingPayment] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.ReadyForOperations,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.ReadyForOperations] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.InProgress,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.InProgress] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.PendingReview
            },
            [SurveyOrderStatus.PendingReview] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.Completed
            }
        };

    private SurveyOrder() { }

    public string OrderNumber { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyRequestId { get; private set; }
    public Guid SurveyServiceId { get; private set; }
    public Guid? SurveyServicePriceId { get; private set; }
    public decimal? ConfirmedSurveyAreaHa { get; private set; }
    public decimal? PricePerHaSnapshot { get; private set; }
    public string? Currency { get; private set; }
    public decimal? FinalPrice { get; private set; }
    public Guid? ScopeConfirmedBy { get; private set; }
    public DateTimeOffset? ScopeConfirmedAt { get; private set; }
    public bool RequiresBaselineMapping { get; private set; }
    public Guid? PreviousCompatibleOrderId { get; private set; }
    public SurveyOrderStatus Status { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyRequest SurveyRequest { get; private set; } = null!;
    public SurveyService SurveyService { get; private set; } = null!;
    public SurveyServicePrice? SurveyServicePrice { get; private set; }
    public SurveyOrder? PreviousCompatibleOrder { get; private set; }
    public ICollection<SurveyAppointment> Appointments { get; private set; } = [];
    public ICollection<SurveyPayment> Payments { get; private set; } = [];

    public static bool CanTransition(SurveyOrderStatus from, SurveyOrderStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                SurveyOrderDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void MarkAwaitingAppointment(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingAppointment, occurredAt);

    public void MarkAwaitingPayment(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingPayment, occurredAt);

    public void MarkReadyForOperations(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.ReadyForOperations, occurredAt);

    public void StartOperations(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.InProgress, occurredAt);

    public void MarkPendingReview(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.PendingReview, occurredAt);

    public void Complete(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.Completed, occurredAt);

    public void Cancel(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.Cancelled, occurredAt);

    private void TransitionTo(SurveyOrderStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            SurveyOrderDomainErrorCodes.InvalidTransition);
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }
}
