using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrder : AggregateRoot
{
    private static readonly Dictionary<SurveyOrderStatus, IReadOnlySet<SurveyOrderStatus>>
        AllowedTransitions = new Dictionary<SurveyOrderStatus, IReadOnlySet<SurveyOrderStatus>>
        {
            [SurveyOrderStatus.PendingBoundaryVerification] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingBaselineAppointment,
                SurveyOrderStatus.AwaitingPricing,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingBaselineAppointment] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.BaselineReady,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.BaselineReady] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.BaselineInProgress,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.BaselineInProgress] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingBaselineReview
            },
            [SurveyOrderStatus.AwaitingBaselineReview] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingPricing
            },
            [SurveyOrderStatus.AwaitingPricing] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingPaidAppointment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingPaidAppointment] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingPayment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingPayment] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.ReadyForPaidService,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.ReadyForPaidService] = new HashSet<SurveyOrderStatus>
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
    public ConfirmedSurveyPoleCount? ConfirmedSurveyPoleCount { get; private set; }
    public PricePerPole? PricePerPoleSnapshot { get; private set; }
    public Guid? FarmBoundaryVersionId { get; private set; }
    public Guid? FarmBaseMapVersionId { get; private set; }
    public Guid? PoleCountConfirmedBy { get; private set; }
    public DateTimeOffset? PoleCountConfirmedAt { get; private set; }
    public Guid? PricingConfirmedBy { get; private set; }
    public DateTimeOffset? PricingConfirmedAt { get; private set; }

    // Legacy compatibility only. Target writes never populate these fields.
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

    public static SurveyOrder Create(
        string orderNumber,
        Guid tenantId,
        Guid farmId,
        Guid surveyRequestId,
        Guid surveyServiceId,
        bool requiresBaselineMapping,
        Guid? previousCompatibleOrderId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(surveyRequestId);
        DomainGuard.NotEmpty(surveyServiceId);
        DomainGuard.Utc(createdAt);

        if (orderNumber.Trim().Length > 40)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderNumber),
                "Order number cannot exceed 40 characters.");
        }

        if (previousCompatibleOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Previous compatible order identifier cannot be empty.",
                nameof(previousCompatibleOrderId));
        }

        return new SurveyOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber.Trim(),
            TenantId = tenantId,
            FarmId = farmId,
            SurveyRequestId = surveyRequestId,
            SurveyServiceId = surveyServiceId,
            RequiresBaselineMapping = requiresBaselineMapping,
            PreviousCompatibleOrderId = previousCompatibleOrderId,
            Status = SurveyOrderStatus.PendingBoundaryVerification,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

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

    public void VerifyBoundaryAndScopeForUnmappedFarm(
        Guid farmBoundaryVersionId,
        Guid verifiedBy,
        DateTimeOffset verifiedAt)
    {
        if (!RequiresBaselineMapping)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.BaselineMappingNotRequired,
                "The order does not require Baseline Mapping.");
        }

        ValidateBoundaryAndScope(farmBoundaryVersionId, verifiedBy, verifiedAt);
        EnsureTransitionTo(SurveyOrderStatus.AwaitingBaselineAppointment, verifiedAt);

        FarmBoundaryVersionId = farmBoundaryVersionId;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        ApplyTransition(SurveyOrderStatus.AwaitingBaselineAppointment, verifiedAt);
    }

    public void VerifyBoundaryScopeAndCurrentInventory(
        Guid farmBoundaryVersionId,
        Guid farmBaseMapVersionId,
        ConfirmedSurveyPoleCount confirmedPoleCount,
        Guid verifiedBy,
        DateTimeOffset verifiedAt)
    {
        if (RequiresBaselineMapping)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.BaselineMappingRequired,
                "The order must complete Baseline Mapping before pricing.");
        }

        ValidateBoundaryAndScope(farmBoundaryVersionId, verifiedBy, verifiedAt);
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        var validatedPoleCount =
            global::AgriDrone.Modules.Surveys.Domain.ConfirmedSurveyPoleCount
                .Create(confirmedPoleCount.Value);
        EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, verifiedAt);

        FarmBoundaryVersionId = farmBoundaryVersionId;
        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        PoleCountConfirmedBy = verifiedBy;
        PoleCountConfirmedAt = verifiedAt;
        ApplyTransition(SurveyOrderStatus.AwaitingPricing, verifiedAt);
    }

    public void MarkBaselineAppointmentConfirmed(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.BaselineReady, occurredAt);

    public void StartBaselineMapping(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.BaselineInProgress, occurredAt);

    public void MarkBaselineCandidatesReady(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingBaselineReview, occurredAt);

    public void RecordBaselinePublication(
        Guid farmBaseMapVersionId,
        ConfirmedSurveyPoleCount confirmedPoleCount,
        Guid confirmedBy,
        DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        DomainGuard.NotEmpty(confirmedBy);
        var validatedPoleCount =
            global::AgriDrone.Modules.Surveys.Domain.ConfirmedSurveyPoleCount
                .Create(confirmedPoleCount.Value);
        EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, confirmedAt);

        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountConfirmedBy = confirmedBy;
        PoleCountConfirmedAt = confirmedAt;
        ApplyTransition(SurveyOrderStatus.AwaitingPricing, confirmedAt);
    }

    public void ConfirmPricing(
        Guid surveyServicePriceId,
        PricePerPole pricePerPole,
        CurrencyCode currency,
        Guid confirmedBy,
        DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(surveyServicePriceId);
        DomainGuard.NotEmpty(confirmedBy);
        EnsureTransitionTo(SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);

        if (currency != CurrencyCode.Vnd)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "Only VND per-pole pricing is supported in the MVP.");
        }

        if (FarmBoundaryVersionId is null ||
            FarmBaseMapVersionId is null ||
            ConfirmedSurveyPoleCount is null)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "Boundary, base-map and confirmed pole-count snapshots are required before pricing.");
        }

        var validatedPrice = global::AgriDrone.Modules.Surveys.Domain.PricePerPole
            .Create(pricePerPole.Amount);

        var finalPrice = Money.CalculateSurveyPrice(
            ConfirmedSurveyPoleCount.Value,
            validatedPrice,
            currency);

        SurveyServicePriceId = surveyServicePriceId;
        PricePerPoleSnapshot = validatedPrice;
        Currency = currency.Value;
        FinalPrice = finalPrice.Amount;
        PricingConfirmedBy = confirmedBy;
        PricingConfirmedAt = confirmedAt;
        ApplyTransition(SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);
    }

    public void MarkPaidAppointmentConfirmed(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingPayment, occurredAt);

    public void MarkPaymentConfirmed(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.ReadyForPaidService, occurredAt);

    public void StartPaidService(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.InProgress, occurredAt);

    public void MarkAnalysisReady(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.PendingReview, occurredAt);

    public void Complete(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.Completed, occurredAt);

    public void Cancel(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.Cancelled, occurredAt);

    private static void ValidateBoundaryAndScope(
        Guid farmBoundaryVersionId,
        Guid verifiedBy,
        DateTimeOffset verifiedAt)
    {
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.NotEmpty(verifiedBy);
        DomainGuard.Utc(verifiedAt);
    }

    private void TransitionTo(SurveyOrderStatus target, DateTimeOffset occurredAt)
    {
        EnsureTransitionTo(target, occurredAt);
        ApplyTransition(target, occurredAt);
    }

    private void EnsureTransitionTo(
        SurveyOrderStatus target,
        DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, target),
            Status,
            target,
            SurveyOrderDomainErrorCodes.InvalidTransition);
    }

    private void ApplyTransition(
        SurveyOrderStatus target,
        DateTimeOffset occurredAt)
    {
        Status = target;
        UpdatedAt = occurredAt;
        Version++;
    }
}
