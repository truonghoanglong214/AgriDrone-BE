using AgriDrone.Modules.Surveys.Application.Abstractions.Pricing;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class SurveyOrder : AggregateRoot
{
    public const decimal DefaultBaselineDepositAmountVnd = 5_000m;
    public const decimal DefaultComboDiscountPercentage = 20.00m;

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
                SurveyOrderStatus.AwaitingBaselineDeposit,
                SurveyOrderStatus.BaselineReady,
                SurveyOrderStatus.AwaitingBoundaryCapture,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingBaselineDeposit] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.BaselineReady,
                SurveyOrderStatus.AwaitingBaselineAppointment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingBoundaryCapture] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.BaselineReady,
                SurveyOrderStatus.AwaitingBaselineAppointment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.BaselineReady] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.BaselineInProgress,
                SurveyOrderStatus.AwaitingBaselineDeposit,
                SurveyOrderStatus.AwaitingBaselineAppointment,
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
                SurveyOrderStatus.AwaitingPaidAppointment,
                SurveyOrderStatus.ReadyForPaidService,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.ReadyForPaidService] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.InProgress,
                SurveyOrderStatus.AwaitingPayment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.InProgress] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.PendingReview
            },
            [SurveyOrderStatus.PendingReview] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.Completed
            },
            [SurveyOrderStatus.AwaitingAppointment] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingDeposit,
                SurveyOrderStatus.ReadyForCapture,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingDeposit] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.ReadyForCapture,
                SurveyOrderStatus.AwaitingAppointment,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.ReadyForCapture] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.Capturing,
                SurveyOrderStatus.AwaitingAppointment,
                SurveyOrderStatus.AwaitingDeposit,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.Capturing] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingCaptureUpload,
                SurveyOrderStatus.AwaitingReflightDecision
            },
            [SurveyOrderStatus.AwaitingCaptureUpload] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingInputReview,
                SurveyOrderStatus.AwaitingReflightDecision,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingInputReview] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingProcessing,
                SurveyOrderStatus.AwaitingReflightDecision,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingProcessing] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.Processing,
                SurveyOrderStatus.AwaitingReflightDecision,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.Processing] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingSettlement,
                SurveyOrderStatus.AwaitingReflightDecision,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingReflightDecision] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.AwaitingAppointment,
                SurveyOrderStatus.AwaitingDeposit,
                SurveyOrderStatus.ReadyForCapture,
                SurveyOrderStatus.Cancelled
            },
            [SurveyOrderStatus.AwaitingSettlement] = new HashSet<SurveyOrderStatus>
            {
                SurveyOrderStatus.Completed,
                SurveyOrderStatus.Cancelled
            }
        };

    private SurveyOrder() { }

    public ContractScope Scope { get; private set; } = ContractScope.Legacy;
    public Guid? BusinessPolicyVersionId { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid SurveyRequestId { get; private set; }
    public Guid SurveyServiceId { get; private set; }
    public Guid? SurveyServicePriceId { get; private set; }
    public ConfirmedSurveyPoleCount? ConfirmedSurveyPoleCount { get; private set; }
    public PricePerPole? PricePerPoleSnapshot { get; private set; }
    public Guid? OriginalSurveyServicePriceId { get; private set; }
    public ConfirmedSurveyPoleCount? OriginalConfirmedPoleCount { get; private set; }
    public PricePerPole? OriginalPricePerPoleSnapshot { get; private set; }
    public decimal? OriginalFinalPrice { get; private set; }
    public Guid? FarmBoundaryVersionId { get; private set; }
    public Guid? FarmBaseMapVersionId { get; private set; }
    public Guid? BoundaryCaptureSessionId { get; private set; }
    public Guid? BaselineRoutePlanId { get; private set; }
    public SurveyPoleCountSource? PoleCountSource { get; private set; }
    public string? PoleCountEvidenceReference { get; private set; }
    public Guid? PoleCountConfirmedBy { get; private set; }
    public DateTimeOffset? PoleCountConfirmedAt { get; private set; }
    public Guid? PricingConfirmedBy { get; private set; }
    public DateTimeOffset? PricingConfirmedAt { get; private set; }

    // Legacy compatibility only. Target writes never populate these fields.
    public decimal? ConfirmedSurveyAreaHa { get; private set; }
    public decimal? PricePerHaSnapshot { get; private set; }

    public string? Currency { get; private set; }
    public decimal? FinalPrice { get; private set; }
    public SurveyPaymentStatus DepositPaymentStatus { get; private set; } = SurveyPaymentStatus.Pending;
    public decimal DepositAmount { get; private set; } = DefaultBaselineDepositAmountVnd;
    public decimal? ComboDiscountPercentage { get; private set; }
    // Registration v2.6. Legacy orders retain their original commercial terms.
    public bool PostFlightSettlement { get; private set; }
    public Guid? BookedServicePriceId { get; private set; }
    public decimal? BookedPricePerPole { get; private set; }
    public SurveyServiceType? BookedServiceType { get; private set; }
    public DateTimeOffset? ServiceReviewedAt { get; private set; }
    public decimal? RemainingAmount { get; private set; }
    public Guid? DepositPaymentId { get; private set; }
    public Guid? DepositObligationId { get; private set; }
    public DateTimeOffset? DepositConfirmedAt { get; private set; }
    public Guid? ScopeConfirmedBy { get; private set; }
    public DateTimeOffset? ScopeConfirmedAt { get; private set; }
    public SurveyScopeType ScopeType { get; private set; } = SurveyScopeType.FarmWide;
    public bool RequiresBaselineMapping { get; private set; }
    public Guid? PreviousCompatibleOrderId { get; private set; }
    public string? PreviousOrderCompatibilityPolicyVersion { get; private set; }
    public string? PreviousOrderCompatibilityReason { get; private set; }
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
    public ICollection<SurveyOrderZoneScope> ZoneScopes { get; private set; } = [];

    public static SurveyOrder Create(
        string orderNumber,
        Guid tenantId,
        Guid farmId,
        Guid surveyRequestId,
        Guid surveyServiceId,
        bool requiresBaselineMapping,
        Guid? previousCompatibleOrderId,
        string previousOrderCompatibilityPolicyVersion,
        string previousOrderCompatibilityReason,
        DateTimeOffset createdAt,
        decimal? customDepositAmount = null,
        decimal? customComboDiscountPercentage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(surveyRequestId);
        DomainGuard.NotEmpty(surveyServiceId);
        DomainGuard.Utc(createdAt);

        var resolvedDepositAmount = customDepositAmount ?? (requiresBaselineMapping
            ? DefaultBaselineDepositAmountVnd
            : 0m);

        if (resolvedDepositAmount < 0m || resolvedDepositAmount != decimal.Truncate(resolvedDepositAmount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(customDepositAmount),
                "Deposit amount must be a non-negative whole VND amount.");
        }

        var resolvedDiscountPercentage = customComboDiscountPercentage ?? (requiresBaselineMapping
            ? DefaultComboDiscountPercentage
            : (decimal?)null);
        if (resolvedDiscountPercentage.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(resolvedDiscountPercentage.Value);
            resolvedDiscountPercentage = decimal.Round(resolvedDiscountPercentage.Value, 2, MidpointRounding.AwayFromZero);
        }

        if (resolvedDiscountPercentage.HasValue && (resolvedDiscountPercentage.Value < 0m || resolvedDiscountPercentage.Value >= 100m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(customComboDiscountPercentage),
                "Combo discount percentage must be at least 0 and less than 100.");
        }

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

        var compatibilityPolicyVersion = NormalizeCompatibilityValue(
            previousOrderCompatibilityPolicyVersion,
            100,
            nameof(previousOrderCompatibilityPolicyVersion));
        var compatibilityReason = NormalizeCompatibilityValue(
            previousOrderCompatibilityReason,
            100,
            nameof(previousOrderCompatibilityReason));

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
            PreviousOrderCompatibilityPolicyVersion =
                compatibilityPolicyVersion,
            PreviousOrderCompatibilityReason = compatibilityReason,
            Status = requiresBaselineMapping
                ? SurveyOrderStatus.AwaitingBaselineAppointment
                : SurveyOrderStatus.PendingBoundaryVerification,
            DepositPaymentStatus = resolvedDepositAmount > 0m
                ? SurveyPaymentStatus.Pending
                : SurveyPaymentStatus.Paid,
            DepositAmount = resolvedDepositAmount,
            ComboDiscountPercentage = resolvedDiscountPercentage,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public static SurveyOrder CreateV3(
        string orderNumber,
        Guid tenantId,
        Guid farmId,
        Guid surveyRequestId,
        Guid surveyServiceId,
        ResolvedSurveyCommercialSnapshot snapshot,
        bool requiresBaselineMapping,
        Guid? farmBaseMapVersionId = null,
        Guid? previousCompatibleOrderId = null,
        string? previousOrderCompatibilityPolicyVersion = null,
        string? previousOrderCompatibilityReason = null,
        DateTimeOffset createdAt = default,
        SurveyOrderStatus initialStatus = SurveyOrderStatus.AwaitingBaselineAppointment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(surveyRequestId);
        DomainGuard.NotEmpty(surveyServiceId);
        DomainGuard.Utc(createdAt);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (orderNumber.Trim().Length > 40)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderNumber),
                "Order number cannot exceed 40 characters.");
        }

        if (!requiresBaselineMapping)
        {
            if (!farmBaseMapVersionId.HasValue || farmBaseMapVersionId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Follow-up survey order that does not require baseline mapping must pin an existing delivered baseline map version.",
                    nameof(farmBaseMapVersionId));
            }
        }
        else
        {
            if (farmBaseMapVersionId.HasValue)
            {
                throw new ArgumentException(
                    "Survey order requiring baseline mapping cannot have a baseline map version pinned at creation.",
                    nameof(farmBaseMapVersionId));
            }
        }

        string? compatibilityPolicyVersion = null;
        string? compatibilityReason = null;
        if (previousCompatibleOrderId.HasValue)
        {
            if (previousCompatibleOrderId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Previous compatible order identifier cannot be empty.",
                    nameof(previousCompatibleOrderId));
            }

            if (previousOrderCompatibilityPolicyVersion is not null)
            {
                compatibilityPolicyVersion = NormalizeCompatibilityValue(
                    previousOrderCompatibilityPolicyVersion,
                    100,
                    nameof(previousOrderCompatibilityPolicyVersion));
            }

            if (previousOrderCompatibilityReason is not null)
            {
                compatibilityReason = NormalizeCompatibilityValue(
                    previousOrderCompatibilityReason,
                    100,
                    nameof(previousOrderCompatibilityReason));
            }
        }

        return new SurveyOrder
        {
            Id = Guid.NewGuid(),
            Scope = ContractScope.V3,
            BusinessPolicyVersionId = snapshot.BusinessPolicyVersionId,
            OrderNumber = orderNumber.Trim(),
            TenantId = tenantId,
            FarmId = farmId,
            SurveyRequestId = surveyRequestId,
            SurveyServiceId = surveyServiceId,
            SurveyServicePriceId = snapshot.PriceVersionId,
            PricePerPoleSnapshot = PricePerPole.Create(snapshot.PricePerPole),
            Currency = snapshot.Currency,
            DepositAmount = snapshot.DepositAmount,
            DepositPaymentStatus = SurveyPaymentStatus.Pending,
            RequiresBaselineMapping = requiresBaselineMapping,
            FarmBaseMapVersionId = farmBaseMapVersionId,
            PreviousCompatibleOrderId = previousCompatibleOrderId,
            PreviousOrderCompatibilityPolicyVersion = compatibilityPolicyVersion,
            PreviousOrderCompatibilityReason = compatibilityReason,
            Status = initialStatus,
            ScopeType = SurveyScopeType.FarmWide,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public static bool CanTransition(SurveyOrderStatus from, SurveyOrderStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public void EnablePostFlightSettlement(SurveyServiceType serviceType, Guid priceId, decimal pricePerPole)
    {
        if (Scope != ContractScope.Legacy)
            throw new InvalidOperationException("V3 orders use their pinned commercial snapshot and capture/settlement lifecycle.");
        if (PostFlightSettlement || FinalPrice.HasValue || Payments.Count != 0)
            throw new InvalidOperationException("Commercial terms must be frozen once, before payment or pricing.");
        if (DepositAmount <= 0m)
            throw new InvalidOperationException("Registration v2.6 requires a positive shared pre-flight deposit.");
        DomainGuard.NotEmpty(priceId);
        _ = PricePerPole.Create(pricePerPole);
        if (serviceType == SurveyServiceType.HarvestReadiness)
            throw new InvalidOperationException("Harvest Readiness is outside registration v2.6.");
        PostFlightSettlement = true;
        BookedServicePriceId = priceId;
        BookedPricePerPole = pricePerPole;
        BookedServiceType = serviceType;
        ComboDiscountPercentage = 0m;
    }

    public void RecordReviewedServicePublication(DateTimeOffset occurredAt)
    {
        if (PostFlightSettlement)
        {
            ApplyPostFlightTransition(SurveyOrderStatus.PendingReview, SurveyOrderStatus.AwaitingPricing, occurredAt);
            ServiceReviewedAt = occurredAt;
        }
        else
            Complete(occurredAt);
    }

    private void ApplyPostFlightTransition(SurveyOrderStatus from, SurveyOrderStatus to, DateTimeOffset occurredAt)
    {
        if (Status != from)
            throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Order in state '{Status}' cannot transition to '{to}'.");
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        ApplyTransition(to, occurredAt);
    }

    public static bool CanTransition(SurveyOrderStatus from, SurveyOrderStatus to, ContractScope scope)
    {
        if (scope == ContractScope.Legacy)
        {
            return IsLegacyStatus(from) && IsLegacyStatus(to) && CanTransition(from, to);
        }

        return IsV3Status(from) && IsV3Status(to) && CanTransition(from, to);
    }

    public static bool IsV3Status(SurveyOrderStatus status) =>
        status is SurveyOrderStatus.AwaitingAppointment
            or SurveyOrderStatus.AwaitingDeposit
            or SurveyOrderStatus.ReadyForCapture
            or SurveyOrderStatus.Capturing
            or SurveyOrderStatus.AwaitingCaptureUpload
            or SurveyOrderStatus.AwaitingInputReview
            or SurveyOrderStatus.AwaitingProcessing
            or SurveyOrderStatus.Processing
            or SurveyOrderStatus.AwaitingReflightDecision
            or SurveyOrderStatus.AwaitingSettlement
            or SurveyOrderStatus.Completed
            or SurveyOrderStatus.Cancelled;

    public static bool IsLegacyStatus(SurveyOrderStatus status) =>
        status is SurveyOrderStatus.PendingBoundaryVerification
            or SurveyOrderStatus.AwaitingBaselineAppointment
            or SurveyOrderStatus.AwaitingBaselineDeposit
            or SurveyOrderStatus.AwaitingBoundaryCapture
            or SurveyOrderStatus.BaselineReady
            or SurveyOrderStatus.BaselineInProgress
            or SurveyOrderStatus.AwaitingBaselineReview
            or SurveyOrderStatus.AwaitingPricing
            or SurveyOrderStatus.AwaitingPaidAppointment
            or SurveyOrderStatus.AwaitingPayment
            or SurveyOrderStatus.ReadyForPaidService
            or SurveyOrderStatus.InProgress
            or SurveyOrderStatus.PendingReview
            or SurveyOrderStatus.Completed
            or SurveyOrderStatus.Cancelled;

    public static SurveyOrder CreateV3CaptureOrder(
        string orderNumber,
        Guid tenantId,
        Guid farmId,
        Guid surveyRequestId,
        Guid surveyServiceId,
        ResolvedSurveyCommercialSnapshot snapshot,
        bool requiresBaselineMapping,
        Guid? farmBaseMapVersionId = null,
        Guid? previousCompatibleOrderId = null,
        string? previousOrderCompatibilityPolicyVersion = null,
        string? previousOrderCompatibilityReason = null,
        DateTimeOffset createdAt = default)
    {
        return CreateV3(
            orderNumber,
            tenantId,
            farmId,
            surveyRequestId,
            surveyServiceId,
            snapshot,
            requiresBaselineMapping,
            farmBaseMapVersionId,
            previousCompatibleOrderId,
            previousOrderCompatibilityPolicyVersion,
            previousOrderCompatibilityReason,
            createdAt,
            initialStatus: SurveyOrderStatus.AwaitingAppointment);
    }

    public void BindDepositObligation(Guid depositObligationId)
    {
        EnsureV3Scope();
        DomainGuard.NotEmpty(depositObligationId, nameof(depositObligationId));
        if (DepositObligationId.HasValue && DepositObligationId.Value != depositObligationId)
        {
            throw new InvalidOperationException($"Deposit obligation is already bound to {DepositObligationId.Value}.");
        }
        DepositObligationId = depositObligationId;
    }

    public void ApplyV3Transition(SurveyOrderStatus target, DateTimeOffset occurredAt)
    {
        EnsureV3Scope();
        EnsureTransitionTo(target, occurredAt);
        ApplyTransition(target, occurredAt);
    }

    public void TransitionToAwaitingAppointmentV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingAppointment, occurredAt);

    public void TransitionToAwaitingDepositV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingDeposit, occurredAt);

    public void TransitionToReadyForCaptureV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.ReadyForCapture, occurredAt);

    public void TransitionToCapturingV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.Capturing, occurredAt);

    public void TransitionToAwaitingCaptureUploadV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingCaptureUpload, occurredAt);

    public void TransitionToAwaitingInputReviewV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingInputReview, occurredAt);

    public void TransitionToAwaitingProcessingV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingProcessing, occurredAt);

    public void TransitionToProcessingV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.Processing, occurredAt);

    public void TransitionToAwaitingReflightDecisionV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingReflightDecision, occurredAt);

    public void TransitionToAwaitingSettlementV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.AwaitingSettlement, occurredAt);

    public void TransitionToCompletedV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.Completed, occurredAt);

    public void TransitionToCancelledV3(DateTimeOffset occurredAt) =>
        ApplyV3Transition(SurveyOrderStatus.Cancelled, occurredAt);

    private void EnsureV3Scope()
    {
        if (Scope != ContractScope.V3)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Order {Id} is not in V3 scope.");
        }
    }

    public void ConfirmFlightScope(
        SurveyScopeType scopeType,
        IReadOnlyCollection<(Guid ZoneId, long ZoneVersion, NetTopologySuite.Geometries.Polygon Boundary)> zones,
        Guid confirmedBy,
        DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(confirmedBy);
        DomainGuard.Utc(confirmedAt);
        ArgumentNullException.ThrowIfNull(zones);

        if (!Enum.IsDefined(scopeType) ||
            (scopeType == SurveyScopeType.FarmWide && zones.Count != 0) ||
            (scopeType == SurveyScopeType.Zones && zones.Count == 0) ||
            zones.Any(zone => zone.ZoneId == Guid.Empty) ||
            zones.Select(zone => zone.ZoneId).Distinct().Count() != zones.Count)
        {
            throw new ArgumentException("The selected Survey scope is invalid.", nameof(zones));
        }

        ZoneScopes.Clear();
        foreach (var zone in zones)
        {
            ZoneScopes.Add(SurveyOrderZoneScope.Create(
                Id, FarmId, zone.ZoneId, zone.ZoneVersion,
                zone.Boundary, confirmedAt));
        }

        ScopeType = scopeType;
        ScopeConfirmedBy = confirmedBy;
        ScopeConfirmedAt = confirmedAt;
    }

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
        EnsureCurrentOrTransitionTo(
            SurveyOrderStatus.AwaitingBaselineAppointment,
            verifiedAt);

        FarmBoundaryVersionId = farmBoundaryVersionId;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        ApplyCurrentOrTransition(
            SurveyOrderStatus.AwaitingBaselineAppointment,
            verifiedAt);
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
        if (!PostFlightSettlement) EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, verifiedAt);
        else
        {
            if (Status != SurveyOrderStatus.PendingBoundaryVerification)
                throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition, "Flight scope is already verified.");
            SurveyTransitionGuard.EnsureTimestamp(verifiedAt, UpdatedAt);
        }

        FarmBoundaryVersionId = farmBoundaryVersionId;
        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountSource = SurveyPoleCountSource.CurrentPublishedInventory;
        PoleCountEvidenceReference = null;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        PoleCountConfirmedBy = verifiedBy;
        PoleCountConfirmedAt = verifiedAt;
        ApplyTransition(PostFlightSettlement ? SurveyOrderStatus.AwaitingPaidAppointment : SurveyOrderStatus.AwaitingPricing, verifiedAt);
    }

    public void VerifyBoundaryAndScope(
        Guid farmBoundaryVersionId,
        Guid? currentFarmBaseMapVersionId,
        ConfirmedSurveyPoleCount? currentConfirmedPoleCount,
        Guid verifiedBy,
        DateTimeOffset verifiedAt)
    {
        ValidateBoundaryAndScope(farmBoundaryVersionId, verifiedBy, verifiedAt);

        if (currentFarmBaseMapVersionId.HasValue !=
            currentConfirmedPoleCount.HasValue)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.BaselineSnapshotIncomplete,
                "A current base map and confirmed pole count must be supplied together.");
        }

        if (!currentFarmBaseMapVersionId.HasValue)
        {
            EnsureCurrentOrTransitionTo(
                SurveyOrderStatus.AwaitingBaselineAppointment,
                verifiedAt);

            FarmBoundaryVersionId = farmBoundaryVersionId;
            ScopeConfirmedBy = verifiedBy;
            ScopeConfirmedAt = verifiedAt;
            RequiresBaselineMapping = true;
            FarmBaseMapVersionId = null;
            ConfirmedSurveyPoleCount = null;
            PoleCountSource = null;
            PoleCountEvidenceReference = null;
            PoleCountConfirmedBy = null;
            PoleCountConfirmedAt = null;
            ApplyCurrentOrTransition(
                SurveyOrderStatus.AwaitingBaselineAppointment,
                verifiedAt);
            return;
        }

        DomainGuard.NotEmpty(currentFarmBaseMapVersionId.Value);
        var validatedPoleCount =
            global::AgriDrone.Modules.Surveys.Domain.ConfirmedSurveyPoleCount
                .Create(currentConfirmedPoleCount!.Value.Value);
        if (!PostFlightSettlement) EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, verifiedAt);
        else
        {
            if (Status != SurveyOrderStatus.PendingBoundaryVerification)
                throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition, "Flight scope is already verified.");
            SurveyTransitionGuard.EnsureTimestamp(verifiedAt, UpdatedAt);
        }

        FarmBoundaryVersionId = farmBoundaryVersionId;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        RequiresBaselineMapping = false;
        FarmBaseMapVersionId = currentFarmBaseMapVersionId.Value;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountSource = SurveyPoleCountSource.CurrentPublishedInventory;
        PoleCountEvidenceReference = null;
        PoleCountConfirmedBy = verifiedBy;
        PoleCountConfirmedAt = verifiedAt;
        ApplyTransition(PostFlightSettlement ? SurveyOrderStatus.AwaitingPaidAppointment : SurveyOrderStatus.AwaitingPricing, verifiedAt);
    }

    public void SetFarmBoundaryFromOrthophoto(
        Guid farmBoundaryVersionId,
        Guid verifiedBy,
        DateTimeOffset verifiedAt)
    {
        DomainGuard.NotEmpty(farmBoundaryVersionId);
        DomainGuard.NotEmpty(verifiedBy);
        DomainGuard.Utc(verifiedAt);
        SurveyTransitionGuard.EnsureTimestamp(verifiedAt, UpdatedAt);

        if (Status is not (
            SurveyOrderStatus.AwaitingBaselineReview or
            SurveyOrderStatus.PendingBoundaryVerification))
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Order in state '{Status}' cannot set farm boundary from orthophoto. Must be in AwaitingBaselineReview.");
        }

        FarmBoundaryVersionId = farmBoundaryVersionId;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        UpdatedAt = verifiedAt;
        Version++;
    }

    public void MarkBaselineAppointmentConfirmed(DateTimeOffset occurredAt)
    {
        var targetStatus = DepositPaymentStatus == SurveyPaymentStatus.Paid
            ? SurveyOrderStatus.BaselineReady
            : SurveyOrderStatus.AwaitingBaselineDeposit;

        TransitionTo(targetStatus, occurredAt);
    }

    public void RecordBaselineDepositConfirmed(
        Guid paymentId,
        decimal amount,
        DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(paymentId);
        DomainGuard.Utc(confirmedAt);
        SurveyTransitionGuard.EnsureTimestamp(confirmedAt, UpdatedAt);

        if (!RequiresBaselineMapping || !CanAcceptDepositPayment())
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Order in state '{Status}' cannot confirm baseline deposit.");
        }

        if (amount != DepositAmount)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Deposit amount must be {DepositAmount} VND.");
        }

        DepositPaymentId = paymentId;
        DepositPaymentStatus = SurveyPaymentStatus.Paid;
        DepositConfirmedAt = confirmedAt;
        if (Status == SurveyOrderStatus.AwaitingBaselineDeposit)
        {
            TransitionTo(SurveyOrderStatus.BaselineReady, confirmedAt);
        }
        else
        {
            if (FinalPrice.HasValue)
                RemainingAmount = Math.Max(0m, FinalPrice.Value - DepositAmount);
            UpdatedAt = confirmedAt;
            Version++;
        }
    }

    public void RecordFollowUpDepositConfirmed(
        Guid paymentId,
        decimal amount,
        DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(paymentId);
        DomainGuard.Utc(confirmedAt);
        SurveyTransitionGuard.EnsureTimestamp(confirmedAt, UpdatedAt);

        if (RequiresBaselineMapping)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                "Order requires baseline mapping. Use RecordBaselineDepositConfirmed instead.");
        }

        if (!CanAcceptDepositPayment())
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                "Deposit is already paid.");
        }

        if (amount != DepositAmount)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Deposit amount must be {DepositAmount} VND.");
        }

        DepositPaymentId = paymentId;
        DepositPaymentStatus = SurveyPaymentStatus.Paid;
        DepositConfirmedAt = confirmedAt;

        if (FinalPrice.HasValue)
        {
            RemainingAmount = Math.Max(0m, FinalPrice.Value - DepositAmount);
        }

        if (PostFlightSettlement && Status == SurveyOrderStatus.AwaitingBaselineDeposit)
        {
            ApplyPostFlightTransition(SurveyOrderStatus.AwaitingBaselineDeposit, SurveyOrderStatus.ReadyForPaidService, confirmedAt);
            return;
        }

        UpdatedAt = confirmedAt;
        Version++;
    }

    public void MarkBaselineReady(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.BaselineReady, occurredAt);

    public void ReopenBaselineAppointment(DateTimeOffset occurredAt)
    {
        TransitionTo(SurveyOrderStatus.AwaitingBaselineAppointment, occurredAt);
        BoundaryCaptureSessionId = null;
        BaselineRoutePlanId = null;
    }

    public void RecordBoundaryCapture(
        Guid boundaryCaptureSessionId,
        Guid farmBoundaryVersionId,
        Guid verifiedBy,
        DateTimeOffset verifiedAt)
    {
        DomainGuard.NotEmpty(boundaryCaptureSessionId);
        if (!RequiresBaselineMapping ||
            (Status != SurveyOrderStatus.AwaitingBoundaryCapture &&
             Status != SurveyOrderStatus.AwaitingBaselineDeposit))
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                "Boundary capture is only valid for an unmapped order awaiting field capture.");
        }

        ValidateBoundaryAndScope(farmBoundaryVersionId, verifiedBy, verifiedAt);
        SurveyTransitionGuard.EnsureTimestamp(verifiedAt, UpdatedAt);
        BoundaryCaptureSessionId = boundaryCaptureSessionId;
        BaselineRoutePlanId = null;
        FarmBoundaryVersionId = farmBoundaryVersionId;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        UpdatedAt = verifiedAt;
        Version++;
    }

    public void ApproveBaselineRoute(
        Guid baselineRoutePlanId,
        Guid boundaryCaptureSessionId,
        Guid farmBoundaryVersionId,
        DateTimeOffset approvedAt)
    {
        DomainGuard.NotEmpty(baselineRoutePlanId);
        DomainGuard.NotEmpty(boundaryCaptureSessionId);
        DomainGuard.NotEmpty(farmBoundaryVersionId);

        if (BoundaryCaptureSessionId != boundaryCaptureSessionId ||
            FarmBoundaryVersionId != farmBoundaryVersionId)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                "The route plan does not match the current capture session and boundary version.");
        }

        EnsureTransitionTo(SurveyOrderStatus.BaselineReady, approvedAt);
        BaselineRoutePlanId = baselineRoutePlanId;
        ApplyTransition(SurveyOrderStatus.BaselineReady, approvedAt);
    }

    public void StartBaselineMapping(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.BaselineInProgress, occurredAt);

    public void MarkBaselineCandidatesReady(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingBaselineReview, occurredAt);

    public void RecordBaselinePublication(
        Guid farmBaseMapVersionId,
        ConfirmedSurveyPoleCount confirmedPoleCount,
        Guid confirmedBy,
        DateTimeOffset confirmedAt,
        Guid? farmBoundaryVersionId = null)
    {
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        DomainGuard.NotEmpty(confirmedBy);
        var validatedPoleCount =
            global::AgriDrone.Modules.Surveys.Domain.ConfirmedSurveyPoleCount
                .Create(confirmedPoleCount.Value);
        EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, confirmedAt);

        if (farmBoundaryVersionId.HasValue && farmBoundaryVersionId.Value != Guid.Empty)
        {
            FarmBoundaryVersionId = farmBoundaryVersionId.Value;
        }

        if (FarmBoundaryVersionId is null || FarmBoundaryVersionId == Guid.Empty)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "Farm boundary version ID is required when recording baseline publication.");
        }

        if (ScopeConfirmedBy is null)
        {
            ScopeConfirmedBy = confirmedBy;
            ScopeConfirmedAt = confirmedAt;
        }

        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountSource = SurveyPoleCountSource.BaselinePublication;
        PoleCountEvidenceReference = null;
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
        EnsurePricingAllowed(confirmedAt);

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
            .Create(PostFlightSettlement ? BookedPricePerPole!.Value : pricePerPole.Amount);

        var finalPrice = Money.CalculateSurveyPrice(
            ConfirmedSurveyPoleCount.Value,
            validatedPrice,
            currency);

        SurveyServicePriceId = PostFlightSettlement ? BookedServicePriceId : surveyServicePriceId;
        PricePerPoleSnapshot = validatedPrice;
        Currency = currency.Value;
        FinalPrice = finalPrice.Amount;
        var depositCredit = DepositPaymentStatus == SurveyPaymentStatus.Paid
            ? DepositAmount
            : 0m;
        RemainingAmount = Math.Max(0m, finalPrice.Amount - depositCredit);
        CaptureOriginalCommercialSnapshot(
            surveyServicePriceId,
            ConfirmedSurveyPoleCount.Value,
            validatedPrice,
            finalPrice.Amount);
        PricingConfirmedBy = confirmedBy;
        PricingConfirmedAt = confirmedAt;
        ApplyTransition(PostFlightSettlement ? SurveyOrderStatus.AwaitingPayment : SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);
    }

    public void ConfirmPoleCountAndPricing(
        Guid farmBaseMapVersionId,
        ConfirmedSurveyPoleCount confirmedPoleCount,
        SurveyPoleCountSource poleCountSource,
        string evidenceReference,
        Guid surveyServicePriceId,
        PricePerPole pricePerPole,
        CurrencyCode currency,
        Guid confirmedBy,
        DateTimeOffset confirmedAt)
    {
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        DomainGuard.NotEmpty(surveyServicePriceId);
        DomainGuard.NotEmpty(confirmedBy);
        DomainGuard.Utc(confirmedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceReference);

        var normalizedEvidence = evidenceReference.Trim();
        if (normalizedEvidence.Length > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceReference),
                "Pole-count evidence reference cannot exceed 1000 characters.");
        }

        if (!Enum.IsDefined(poleCountSource))
        {
            throw new ArgumentOutOfRangeException(nameof(poleCountSource));
        }

        EnsurePricingAllowed(confirmedAt);

        if (currency != CurrencyCode.Vnd)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "Only VND per-pole pricing is supported in the MVP.");
        }

        if (FarmBoundaryVersionId is null || ScopeConfirmedBy is null)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "An approved boundary and verified scope are required before pricing.");
        }

        var validatedPoleCount =
            global::AgriDrone.Modules.Surveys.Domain.ConfirmedSurveyPoleCount
                .Create(confirmedPoleCount.Value);
        var validatedPrice = global::AgriDrone.Modules.Surveys.Domain.PricePerPole
            .Create(PostFlightSettlement ? BookedPricePerPole!.Value : pricePerPole.Amount);
        var originalPrice = Money.CalculateSurveyPrice(
            validatedPoleCount,
            validatedPrice,
            currency);

        var effectivePricePerPole = GetEffectivePricePerPole(validatedPrice);
        var finalPriceAmount = Money.CalculateSurveyPrice(
            validatedPoleCount, effectivePricePerPole, currency).Amount;

        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountSource = poleCountSource;
        PoleCountEvidenceReference = normalizedEvidence;
        PoleCountConfirmedBy = confirmedBy;
        PoleCountConfirmedAt = confirmedAt;
        SurveyServicePriceId = PostFlightSettlement ? BookedServicePriceId : surveyServicePriceId;
        PricePerPoleSnapshot = effectivePricePerPole;
        Currency = currency.Value;
        FinalPrice = finalPriceAmount;
        var depositCredit = DepositPaymentStatus == SurveyPaymentStatus.Paid
            ? DepositAmount
            : 0m;
        RemainingAmount = Math.Max(0m, finalPriceAmount - depositCredit);
        CaptureOriginalCommercialSnapshot(
            surveyServicePriceId,
            validatedPoleCount,
            validatedPrice,
            originalPrice.Amount);
        PricingConfirmedBy = confirmedBy;
        PricingConfirmedAt = confirmedAt;
        ApplyTransition(PostFlightSettlement ? SurveyOrderStatus.AwaitingPayment : SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);
    }

    private void EnsurePricingAllowed(DateTimeOffset confirmedAt)
    {
        if (!PostFlightSettlement)
        {
            EnsureTransitionTo(SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);
            return;
        }
        SurveyTransitionGuard.EnsureTimestamp(confirmedAt, UpdatedAt);
        if (Status != SurveyOrderStatus.AwaitingPricing || ServiceReviewedAt is null ||
            BookedServicePriceId is null || BookedPricePerPole is null ||
            DepositPaymentStatus != SurveyPaymentStatus.Paid)
            throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition,
                "Final pricing requires post-flight Manager-reviewed results, frozen booking terms and a paid deposit.");
    }

    public void MarkPaidAppointmentConfirmed(DateTimeOffset occurredAt)
    {
        if (PostFlightSettlement)
        {
            if (ServiceReviewedAt is not null)
                throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition, "The survey has already been reviewed.");
            var target = DepositPaymentStatus == SurveyPaymentStatus.Paid
                ? SurveyOrderStatus.ReadyForPaidService : SurveyOrderStatus.AwaitingBaselineDeposit;
            ApplyPostFlightTransition(Status == SurveyOrderStatus.AwaitingPricing
                ? SurveyOrderStatus.AwaitingPricing : SurveyOrderStatus.AwaitingPaidAppointment, target, occurredAt);
        }
        else TransitionTo(SurveyOrderStatus.AwaitingPayment, occurredAt);
    }

    public void ReopenPaidAppointment(DateTimeOffset occurredAt)
    {
        if (PostFlightSettlement)
        {
            if (ServiceReviewedAt is not null || Status is not (SurveyOrderStatus.ReadyForPaidService or SurveyOrderStatus.AwaitingBaselineDeposit))
                throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition, "Only an unflown service appointment can be reopened.");
            ApplyPostFlightTransition(Status, SurveyOrderStatus.AwaitingPaidAppointment, occurredAt);
        }
        else TransitionTo(SurveyOrderStatus.AwaitingPaidAppointment, occurredAt);
    }

    public void MarkPaymentConfirmed(DateTimeOffset occurredAt)
    {
        if (Scope == ContractScope.V3)
            throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition,
                "V3 settlement requires the reviewed-result release coordinator.");
        if (PostFlightSettlement)
        {
            if (ServiceReviewedAt is null || FinalPrice is not > 0m ||
                ConfirmedSurveyPoleCount is null || PricePerPoleSnapshot is null ||
                RemainingAmount is null or < 0m || DepositPaymentStatus != SurveyPaymentStatus.Paid)
                throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition,
                    "Reviewed post-flight results, a finalized invoice and a paid deposit are required before settlement.");
            ApplyPostFlightTransition(SurveyOrderStatus.AwaitingPayment, SurveyOrderStatus.Completed, occurredAt);
        }
        else TransitionTo(SurveyOrderStatus.ReadyForPaidService, occurredAt);
    }

    public bool CanAcceptDepositPayment() => DepositAmount > 0m &&
        DepositPaymentStatus != SurveyPaymentStatus.Paid &&
        (PostFlightSettlement
            ? (Status is SurveyOrderStatus.AwaitingBaselineDeposit or SurveyOrderStatus.PendingBoundaryVerification
                or SurveyOrderStatus.AwaitingPaidAppointment or SurveyOrderStatus.AwaitingBaselineAppointment ||
                (Status == SurveyOrderStatus.AwaitingPayment && DepositPaymentStatus == SurveyPaymentStatus.Refunded))
            : RequiresBaselineMapping
            ? Status == SurveyOrderStatus.AwaitingBaselineDeposit ||
              (DepositPaymentStatus == SurveyPaymentStatus.Refunded &&
               Status is SurveyOrderStatus.AwaitingPricing or SurveyOrderStatus.AwaitingPaidAppointment
                   or SurveyOrderStatus.AwaitingPayment)
            : Status is SurveyOrderStatus.PendingBoundaryVerification or SurveyOrderStatus.AwaitingPricing
                or SurveyOrderStatus.AwaitingPaidAppointment or SurveyOrderStatus.AwaitingPayment);

    public bool IsZeroBalanceSettled() =>
        FinalPrice is > 0 && RemainingAmount == 0m &&
        DepositPaymentStatus == SurveyPaymentStatus.Paid &&
        Status is SurveyOrderStatus.ReadyForPaidService or SurveyOrderStatus.InProgress
            or SurveyOrderStatus.PendingReview or SurveyOrderStatus.Completed;

    public PricePerPole GetEffectivePricePerPole(PricePerPole cataloguePrice)
    {
        if (PostFlightSettlement) return PricePerPole.Create(BookedPricePerPole!.Value);
        var percentage = ComboDiscountPercentage ?? 0m;
        if (percentage < 0m || percentage >= 100m)
            throw new SurveyDomainException(SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "The saved Combo discount must be at least 0 and less than 100.");
        // Checkout accepts whole VND; preserve the positive per-pole invariant.
        var amount = decimal.Round(cataloguePrice.Amount * (1m - percentage / 100m),
            0, MidpointRounding.AwayFromZero);
        return PricePerPole.Create(Math.Max(1m, amount));
    }

    public void RevokePaidServiceReadiness(DateTimeOffset occurredAt)
    {
        if (PostFlightSettlement && Status == SurveyOrderStatus.Completed)
            ApplyPostFlightTransition(SurveyOrderStatus.Completed, SurveyOrderStatus.AwaitingPayment, occurredAt);
        else if (Status == SurveyOrderStatus.ReadyForPaidService)
            TransitionTo(SurveyOrderStatus.AwaitingPayment, occurredAt);
    }

    public void RevokeBaselineDeposit(DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        DepositPaymentStatus = SurveyPaymentStatus.Refunded;
        DepositPaymentId = null;
        DepositConfirmedAt = null;

        if (Status == SurveyOrderStatus.BaselineReady)
        {
            TransitionTo(SurveyOrderStatus.AwaitingBaselineDeposit, occurredAt);
        }
        else
        {
            if (FinalPrice.HasValue)
            {
                RemainingAmount = FinalPrice.Value;
            }
            if (PostFlightSettlement && Status == SurveyOrderStatus.ReadyForPaidService)
                Status = SurveyOrderStatus.AwaitingBaselineDeposit;
            else if (Status == SurveyOrderStatus.ReadyForPaidService || (PostFlightSettlement && Status == SurveyOrderStatus.Completed))
                Status = SurveyOrderStatus.AwaitingPayment;
            UpdatedAt = occurredAt;
            Version++;
        }
    }

    public void ApplyPriceAdjustment(
        Guid surveyServicePriceId,
        ConfirmedSurveyPoleCount confirmedPoleCount,
        PricePerPole pricePerPole,
        CurrencyCode currency,
        Guid appliedBy,
        DateTimeOffset appliedAt)
    {
        DomainGuard.NotEmpty(surveyServicePriceId);
        DomainGuard.NotEmpty(appliedBy);
        DomainGuard.Utc(appliedAt);
        SurveyTransitionGuard.EnsureTimestamp(appliedAt, UpdatedAt);

        if (Status is not (
            SurveyOrderStatus.AwaitingPaidAppointment or
            SurveyOrderStatus.AwaitingPayment or
            SurveyOrderStatus.ReadyForPaidService))
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.InvalidTransition,
                $"Order in state '{Status}' cannot apply a price adjustment.");
        }

        if (FarmBoundaryVersionId is null || FarmBaseMapVersionId is null)
        {
            throw new SurveyDomainException(
                SurveyOrderDomainErrorCodes.PricingSnapshotIncomplete,
                "Boundary and base-map snapshots are required for a price adjustment.");
        }

        var finalPrice = Money.CalculateSurveyPrice(
            confirmedPoleCount,
            pricePerPole,
            currency);
        SurveyServicePriceId = surveyServicePriceId;
        ConfirmedSurveyPoleCount = confirmedPoleCount;
        PricePerPoleSnapshot = pricePerPole;
        Currency = currency.Value;
        FinalPrice = finalPrice.Amount;
        var depositCredit = DepositPaymentStatus == SurveyPaymentStatus.Paid
            ? DepositAmount
            : 0m;
        RemainingAmount = Math.Max(0m, finalPrice.Amount - depositCredit);
        PoleCountConfirmedBy = appliedBy;
        PoleCountConfirmedAt = appliedAt;
        PricingConfirmedBy = appliedBy;
        PricingConfirmedAt = appliedAt;
        UpdatedAt = appliedAt;

        if (Status == SurveyOrderStatus.ReadyForPaidService)
        {
            Status = SurveyOrderStatus.AwaitingPayment;
        }

        Version++;
    }

    private void CaptureOriginalCommercialSnapshot(
        Guid surveyServicePriceId,
        ConfirmedSurveyPoleCount poleCount,
        PricePerPole pricePerPole,
        decimal finalPrice)
    {
        if (OriginalFinalPrice.HasValue)
        {
            return;
        }

        OriginalSurveyServicePriceId = surveyServicePriceId;
        OriginalConfirmedPoleCount = poleCount;
        OriginalPricePerPoleSnapshot = pricePerPole;
        OriginalFinalPrice = finalPrice;
    }

    public void StartPaidService(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.InProgress, occurredAt);

    public void MarkAnalysisReady(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.PendingReview, occurredAt);

    public void Complete(DateTimeOffset occurredAt)
    {
        if (Scope == ContractScope.V3)
            throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition,
                "V3 completion requires atomic reviewed-result release and financial settlement.");
        if (PostFlightSettlement)
            throw new SurveyDomainException(SurveyOrderDomainErrorCodes.InvalidTransition, "Post-flight orders complete through final settlement, after review.");
        TransitionTo(SurveyOrderStatus.Completed, occurredAt);
    }

    public void RecordPublishedPlantHealthComparison(Guid? previousOrderId, string policyVersion, string reason)
    {
        if (Status != SurveyOrderStatus.PendingReview || previousOrderId == Guid.Empty || previousOrderId == Id)
            throw new InvalidOperationException("Comparison must be resolved for the current pending publication.");
        var policy = NormalizeCompatibilityValue(policyVersion, 100, nameof(policyVersion));
        var decision = NormalizeCompatibilityValue(reason, 100, nameof(reason));
        if ((previousOrderId.HasValue && decision != "COMPATIBLE") || (!previousOrderId.HasValue && decision == "COMPATIBLE"))
            throw new InvalidOperationException("Comparison identity and compatibility reason must agree.");
        PreviousCompatibleOrderId = previousOrderId;
        PreviousOrderCompatibilityPolicyVersion = policy;
        PreviousOrderCompatibilityReason = decision;
    }

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

    private void EnsureCurrentOrTransitionTo(
        SurveyOrderStatus target,
        DateTimeOffset occurredAt)
    {
        SurveyTransitionGuard.EnsureTimestamp(occurredAt, UpdatedAt);
        if (Status != target)
        {
            SurveyTransitionGuard.EnsureAllowed(
                CanTransition(Status, target),
                Status,
                target,
                SurveyOrderDomainErrorCodes.InvalidTransition);
        }
    }

    private void ApplyCurrentOrTransition(
        SurveyOrderStatus target,
        DateTimeOffset occurredAt)
    {
        if (Status == target)
        {
            UpdatedAt = occurredAt;
            Version++;
            return;
        }

        ApplyTransition(target, occurredAt);
    }

    private static string NormalizeCompatibilityValue(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Compatibility value cannot exceed {maximumLength} characters.");
        }

        return normalized;
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
