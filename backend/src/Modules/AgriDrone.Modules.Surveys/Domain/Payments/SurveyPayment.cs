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
                SurveyPaymentStatus.Paid,
                SurveyPaymentStatus.Failed,
                SurveyPaymentStatus.AdjustmentRequired
            },
            [SurveyPaymentStatus.Processing] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.Paid,
                SurveyPaymentStatus.Failed,
                SurveyPaymentStatus.AdjustmentRequired
            },
            [SurveyPaymentStatus.Paid] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.AdjustmentRequired,
                SurveyPaymentStatus.Refunded
            },
            [SurveyPaymentStatus.AdjustmentRequired] = new HashSet<SurveyPaymentStatus>
            {
                SurveyPaymentStatus.Paid,
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
    public PaymentPurpose Purpose { get; private set; } = PaymentPurpose.FinalBalance;
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public uint Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public SurveyOrder SurveyOrder { get; private set; } = null!;
    public ICollection<PaymentEvent> Events { get; private set; } = [];

    public static SurveyPayment Create(
        Guid surveyOrderId,
        Money amount,
        string provider,
        DateTimeOffset createdAt,
        PaymentPurpose purpose = PaymentPurpose.FinalBalance)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be positive.");
        }

        var normalizedProvider = provider.Trim().ToLowerInvariant();
        if (normalizedProvider.Length > 50)
        {
            throw new ArgumentOutOfRangeException(
                nameof(provider),
                "Payment provider cannot exceed 50 characters.");
        }

        return new SurveyPayment
        {
            Id = Guid.CreateVersion7(),
            SurveyOrderId = surveyOrderId,
            Amount = amount.Amount,
            Currency = amount.Currency.Value,
            Provider = normalizedProvider,
            Status = SurveyPaymentStatus.Pending,
            Purpose = purpose,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

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

    public void StartProcessing(
        string providerReference,
        DateTimeOffset occurredAt)
    {
        SetProviderReference(providerReference);
        TransitionTo(SurveyPaymentStatus.Processing, occurredAt);
    }

    // Compatibility for legacy in-memory callers. Target persistence assigns
    // the provider reference through the overload above.
    public void StartProcessing(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.Processing, occurredAt);

    public void Confirm(string providerReference, DateTimeOffset confirmedAt)
    {
        SetProviderReference(providerReference);
        TransitionTo(SurveyPaymentStatus.Paid, confirmedAt);
        ConfirmedAt = confirmedAt;
    }

    public void Fail(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.Failed, occurredAt);

    public void RequireAdjustment(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.AdjustmentRequired, occurredAt);

    public void Refund(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyPaymentStatus.Refunded, occurredAt);

    public void Reconcile(
        Money settledAmount,
        string providerReference,
        DateTimeOffset reconciledAt)
    {
        if (Status is not (
            SurveyPaymentStatus.Pending or
            SurveyPaymentStatus.Processing or
            SurveyPaymentStatus.AdjustmentRequired))
        {
            throw new SurveyDomainException(
                PaymentDomainErrorCodes.InvalidTransition,
                $"Payment in state '{Status}' cannot be reconciled.");
        }

        SetProviderReference(providerReference);
        Amount = settledAmount.Amount;
        Currency = settledAmount.Currency.Value;
        TransitionTo(SurveyPaymentStatus.Paid, reconciledAt);
        ConfirmedAt = reconciledAt;
    }

    private void SetProviderReference(string providerReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerReference);
        var normalizedReference = providerReference.Trim();
        if (normalizedReference.Length > 150)
        {
            throw new ArgumentOutOfRangeException(
                nameof(providerReference),
                "Payment provider reference cannot exceed 150 characters.");
        }

        if (ProviderReference is not null &&
            !string.Equals(
                ProviderReference,
                normalizedReference,
                StringComparison.Ordinal))
        {
            throw new SurveyDomainException(
                PaymentDomainErrorCodes.InvalidTransition,
                "The provider reference cannot change after payment initiation.");
        }

        ProviderReference = normalizedReference;
    }

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
