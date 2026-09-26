using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyPayment : Entity
{
    private static readonly Dictionary<SurveyPaymentStatus, IReadOnlySet<SurveyPaymentStatus>>
        AllowedTransitions = new Dictionary<SurveyPaymentStatus, IReadOnlySet<SurveyPaymentStatus>>
        {
            [SurveyPaymentStatus.Pending] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.Processing,
                SurveyPaymentStatus.Confirmed,
                SurveyPaymentStatus.Failed
            },
            [SurveyPaymentStatus.Processing] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.Confirmed,
                SurveyPaymentStatus.Failed
            },
            [SurveyPaymentStatus.Confirmed] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.AdjustmentRequired,
                SurveyPaymentStatus.Refunded
            },
            [SurveyPaymentStatus.AdjustmentRequired] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.Confirmed,
                SurveyPaymentStatus.Refunded
            }
        };

    private SurveyPayment() { }

    public Guid SurveyOrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string? ProviderReference { get; private set; }
    public SurveyPaymentStatus Status { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;
    public ICollection<PaymentEvent> Events { get; private set; } = [];

    public static bool CanTransition(SurveyPaymentStatus from, SurveyPaymentStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                PaymentDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public void StartProcessing(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.Processing, occurredAt);

    public void Confirm(string providerReference, DateTimeOffset confirmedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);
        TransitionTo(SurveyPaymentStatus.Confirmed, confirmedAt);
        ProviderReference = providerReference.Trim();
        ConfirmedAt = confirmedAt;
    }

    public void Fail(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.Failed, occurredAt);

    public void RequireAdjustment(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.AdjustmentRequired, occurredAt);

    public void Refund(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.Refunded, occurredAt);

    private void TransitionTo(SurveyPaymentStatus target, DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            PaymentDomainErrorCodes.InvalidTransition);
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }
}
