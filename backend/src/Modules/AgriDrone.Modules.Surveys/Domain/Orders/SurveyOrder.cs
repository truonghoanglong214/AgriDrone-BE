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
    public decimal DepositAmount { get; private set; } = 10_000m;
    public decimal? RemainingAmount { get; private set; }
    public Guid? DepositPaymentId { get; private set; }
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
            DepositPaymentStatus = requiresBaselineMapping
                ? SurveyPaymentStatus.Pending
                : SurveyPaymentStatus.Paid,
            DepositAmount = requiresBaselineMapping ? 10_000m : 0m,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    public static bool CanTransition(SurveyOrderStatus from, SurveyOrderStatus to) =>
        AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

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
        EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, verifiedAt);

        FarmBoundaryVersionId = farmBoundaryVersionId;
        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountSource = SurveyPoleCountSource.CurrentPublishedInventory;
        PoleCountEvidenceReference = null;
        ScopeConfirmedBy = verifiedBy;
        ScopeConfirmedAt = verifiedAt;
        PoleCountConfirmedBy = verifiedBy;
        PoleCountConfirmedAt = verifiedAt;
        ApplyTransition(SurveyOrderStatus.AwaitingPricing, verifiedAt);
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
        EnsureTransitionTo(SurveyOrderStatus.AwaitingPricing, verifiedAt);

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
        ApplyTransition(SurveyOrderStatus.AwaitingPricing, verifiedAt);
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

        if (Status != SurveyOrderStatus.AwaitingBaselineDeposit)
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
        TransitionTo(SurveyOrderStatus.BaselineReady, confirmedAt);
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
        var depositCredit = (RequiresBaselineMapping && DepositPaymentStatus == SurveyPaymentStatus.Paid)
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
        ApplyTransition(SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);
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

        EnsureTransitionTo(SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);

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
            .Create(pricePerPole.Amount);
        var finalPrice = Money.CalculateSurveyPrice(
            validatedPoleCount,
            validatedPrice,
            currency);

        FarmBaseMapVersionId = farmBaseMapVersionId;
        ConfirmedSurveyPoleCount = validatedPoleCount;
        PoleCountSource = poleCountSource;
        PoleCountEvidenceReference = normalizedEvidence;
        PoleCountConfirmedBy = confirmedBy;
        PoleCountConfirmedAt = confirmedAt;
        SurveyServicePriceId = surveyServicePriceId;
        PricePerPoleSnapshot = validatedPrice;
        Currency = currency.Value;
        FinalPrice = finalPrice.Amount;
        var depositCredit = (RequiresBaselineMapping && DepositPaymentStatus == SurveyPaymentStatus.Paid)
            ? DepositAmount
            : 0m;
        RemainingAmount = Math.Max(0m, finalPrice.Amount - depositCredit);
        CaptureOriginalCommercialSnapshot(
            surveyServicePriceId,
            validatedPoleCount,
            validatedPrice,
            finalPrice.Amount);
        PricingConfirmedBy = confirmedBy;
        PricingConfirmedAt = confirmedAt;
        ApplyTransition(SurveyOrderStatus.AwaitingPaidAppointment, confirmedAt);
    }

    public void MarkPaidAppointmentConfirmed(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingPayment, occurredAt);

    public void ReopenPaidAppointment(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.AwaitingPaidAppointment, occurredAt);

    public void MarkPaymentConfirmed(DateTimeOffset occurredAt) =>
        TransitionTo(SurveyOrderStatus.ReadyForPaidService, occurredAt);

    public void RevokePaidServiceReadiness(DateTimeOffset occurredAt)
    {
        if (Status == SurveyOrderStatus.ReadyForPaidService)
        {
            TransitionTo(SurveyOrderStatus.AwaitingPayment, occurredAt);
        }
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
        var depositCredit = (RequiresBaselineMapping && DepositPaymentStatus == SurveyPaymentStatus.Paid)
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
