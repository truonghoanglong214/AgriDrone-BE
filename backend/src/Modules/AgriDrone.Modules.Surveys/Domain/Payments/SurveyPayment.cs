using AgriDrone.SharedKernel.Application;
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
    public Guid? DepositObligationId { get; private set; }
    public Guid? InvoiceRevisionId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string? ProviderReference { get; private set; }
    public SurveyPaymentStatus Status { get; private set; }
    public PaymentPurpose Purpose { get; private set; } = PaymentPurpose.FinalBalance;
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? InvalidatedAt { get; private set; }
    public Guid? InvalidatedBy { get; private set; }
    public string? InvalidationReason { get; private set; }
    public Guid? SupersededByInvoiceRevisionId { get; private set; }
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
        PaymentPurpose purpose = PaymentPurpose.FinalBalance,
        Guid? depositObligationId = null)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (depositObligationId.HasValue)
        {
            DomainGuard.NotEmpty(depositObligationId.Value, nameof(depositObligationId));
        }

        if ((purpose == PaymentPurpose.OrderDeposit) != depositObligationId.HasValue)
        {
            throw new ArgumentException(
                "ORDER_DEPOSIT payments must reference the pinned deposit obligation, and other purposes must not.",
                nameof(depositObligationId));
        }
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
            DepositObligationId = depositObligationId,
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

    public Result BindInvoiceRevision(Guid invoiceRevisionId)
    {
        if (Purpose != PaymentPurpose.FinalBalance)
        {
            return Result.Failure(AppError.Validation(
                "SurveyPayment.InvoiceBindingInvalidPurpose",
                "Only FinalBalance payments can bind to an invoice revision."));
        }

        if (invoiceRevisionId == Guid.Empty)
        {
            return Result.Failure(AppError.Validation(
                "SurveyPayment.InvoiceRevisionIdRequired",
                "InvoiceRevisionId is required."));
        }

        if (InvoiceRevisionId.HasValue && InvoiceRevisionId.Value != invoiceRevisionId)
        {
            return Result.Failure(AppError.Conflict(
                "SurveyPayment.InvoiceRevisionAlreadyBound",
                "Payment is already bound to a different invoice revision."));
        }

        InvoiceRevisionId = invoiceRevisionId;
        return Result.Success();
    }

    public Result InvalidateForInvoiceCorrection(
        Guid actorId,
        DateTimeOffset invalidatedAt,
        string reason,
        Guid? supersededByInvoiceRevisionId)
    {
        if (actorId == Guid.Empty)
        {
            return Result.Failure(AppError.Validation(
                "ActorId.Required",
                "Actor identifier is required to invalidate checkout obligation."));
        }

        if (invalidatedAt == default || invalidatedAt.Offset != TimeSpan.Zero)
        {
            return Result.Failure(AppError.Validation(
                "InvalidatedAt.Invalid",
                "InvalidatedAt must be a valid UTC timestamp."));
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
        {
            return Result.Failure(AppError.Validation(
                "InvalidationReason.Invalid",
                "Invalidation reason must be between 1 and 500 characters."));
        }

        if (supersededByInvoiceRevisionId.HasValue && supersededByInvoiceRevisionId.Value == Guid.Empty)
        {
            return Result.Failure(AppError.Validation(
                "SupersededByInvoiceRevisionId.Invalid",
                "SupersededByInvoiceRevisionId cannot be empty when supplied."));
        }

        if (Status == SurveyPaymentStatus.Invalidated)
        {
            if (InvalidatedBy == actorId &&
                InvalidatedAt == invalidatedAt &&
                string.Equals(InvalidationReason, normalizedReason, StringComparison.Ordinal) &&
                SupersededByInvoiceRevisionId == supersededByInvoiceRevisionId)
            {
                return Result.Success();
            }

            return Result.Failure(AppError.Conflict(
                "SurveyPayment.AlreadyInvalidated",
                "Checkout payment has already been invalidated with different audit metadata."));
        }

        if (Status is not (
            SurveyPaymentStatus.Pending or
            SurveyPaymentStatus.Processing or
            SurveyPaymentStatus.AdjustmentRequired))
        {
            return Result.Failure(AppError.Conflict(
                "SurveyPayment.CannotInvalidate",
                $"Checkout payment in status '{Status}' cannot be invalidated by invoice correction."));
        }

        if (invalidatedAt < UpdatedAt)
        {
            return Result.Failure(AppError.Validation(
                "InvalidatedAt.BeforeUpdatedAt",
                "InvalidatedAt cannot be earlier than payment UpdatedAt."));
        }

        Status = SurveyPaymentStatus.Invalidated;
        InvalidatedAt = invalidatedAt;
        InvalidatedBy = actorId;
        InvalidationReason = normalizedReason;
        SupersededByInvoiceRevisionId = supersededByInvoiceRevisionId;
        UpdatedAt = invalidatedAt;
        Version++;
        return Result.Success();
    }

    /// <summary>
    /// Preserves an external provider reference returned after a concurrent pre-release invoice
    /// correction invalidated a pending V3 FinalBalance checkout, keeping the checkout Invalidated
    /// so late callbacks can still route into reconciliation without crediting replacement invoices.
    /// </summary>
    public Result AttachProviderReferenceForInvalidatedCheckout(
        string providerReference,
        DateTimeOffset occurredAt)
    {
        if (Status != SurveyPaymentStatus.Invalidated)
        {
            return Result.Failure(AppError.Conflict(
                "SurveyPayment.NotInvalidated",
                "Only an already invalidated checkout can preserve a late provider reference."));
        }

        var normalizedReference = providerReference?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReference) || normalizedReference.Length > 150)
        {
            return Result.Failure(AppError.Validation(
                "SurveyPayment.ProviderReferenceInvalid",
                "ProviderReference must be between 1 and 150 characters."));
        }

        if (occurredAt == default || occurredAt.Offset != TimeSpan.Zero)
        {
            return Result.Failure(AppError.Validation(
                "OccurredAt.Invalid",
                "OccurredAt must be a valid UTC timestamp."));
        }

        if (ProviderReference is not null)
        {
            return string.Equals(ProviderReference, normalizedReference, StringComparison.Ordinal)
                ? Result.Success()
                : Result.Failure(AppError.Conflict(
                    "SurveyPayment.ProviderReferenceMismatch",
                    "Invalidated checkout already holds a different provider reference."));
        }

        ProviderReference = normalizedReference;
        if (occurredAt > UpdatedAt)
        {
            UpdatedAt = occurredAt;
        }
        Version++;
        return Result.Success();
    }

    public Result BindBalanceCheckoutReference(string? providerReference)
    {
        if (Purpose != PaymentPurpose.FinalBalance || InvoiceRevisionId is null ||
            Status != SurveyPaymentStatus.Pending || ProviderReference is not null)
        {
            return Result.Failure(AppError.Conflict(
                "SurveyPayment.InvalidBalanceIntent",
                "Only a pending invoice-bound balance intent without a reference can bind a provider reference."));
        }

        var reference = providerReference?.Trim();
        if (string.IsNullOrEmpty(reference) || reference.Length > 150)
        {
            return Result.Failure(AppError.Validation(
                "SurveyPayment.ProviderReferenceInvalid",
                "ProviderReference must be between 1 and 150 characters."));
        }

        SetProviderReference(reference);
        return Result.Success();
    }

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
