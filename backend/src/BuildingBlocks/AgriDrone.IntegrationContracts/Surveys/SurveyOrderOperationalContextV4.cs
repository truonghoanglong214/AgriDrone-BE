namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record OperationalAdjustmentDetail(
    Guid Id,
    string Reason,
    string Status,
    decimal OldFinalPrice,
    decimal NewFinalPrice,
    string Currency,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record OperationalSettlementDetail(
    Guid Id,
    Guid PriceAdjustmentId,
    string Type,
    string Status,
    decimal Amount,
    string Currency,
    string? FailureReason,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record OperationalExtraFlightDetail(
    Guid Id,
    string Reason,
    string Status,
    decimal CostImpact,
    string Currency,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record CommercialReadinessEvaluation(
    bool IsReady,
    IReadOnlyList<string> Failures);

public sealed record OperationalMissionReadiness(
    bool IsMissionPrepared,
    bool IsMissionScheduled,
    bool IsDroneReady,
    bool IsPreflightCompleted,
    bool SafetyChecksSatisfied);

public sealed record PrimaryManagerOperationalContext(
    Guid? ManagerProfileId,
    Guid? AssignmentId,
    uint? AssignmentVersion,
    bool IsActive,
    bool IsQualified,
    bool IsAvailable);

/// <summary>
/// V4 Operational Context: Decouples BE1 commercial/authorization readiness from BE2 hardware/drone readiness,
/// while providing comprehensive versions, price snapshots, and operational references.
/// </summary>
public sealed record SurveyOrderOperationalContextV4(
    Guid SurveyOrderId,
    Guid TenantId,
    Guid FarmId,
    string ServiceCode,
    string ServiceType,
    string RequestedMissionPurpose,
    string OrderStatus,
    uint OrderVersion,
    Guid? FarmBoundaryVersionId,
    int? FarmBoundaryVersionNumber,
    Guid? FarmBaseMapVersionId,
    int? FarmBaseMapVersionNumber,
    bool RequiresBaselineMapping,
    IReadOnlyList<Guid>? ScopeZoneIds,
    DateTimeOffset? ScopeConfirmedAt,
    int? OriginalSurveyPoleCount,
    int? ConfirmedSurveyPoleCount,
    Guid? SurveyServicePriceId,
    decimal? OriginalPricePerPole,
    decimal? PricePerPole,
    string? Currency,
    decimal? OriginalTotalPrice,
    decimal? FinalPrice,
    Guid? AppointmentId,
    string? AppointmentPurpose,
    DateTimeOffset? AppointmentStartAt,
    DateTimeOffset? AppointmentEndAt,
    string? AppointmentStatus,
    uint? AppointmentVersion,
    Guid? PaymentId,
    string? PaymentStatus,
    decimal? PaidAmount,
    string? PaidCurrency,
    bool IsPaymentAmountAndCurrencyMatched,
    PrimaryManagerOperationalContext ManagerContext,
    Guid? PreviousCompatibleOrderId,
    Guid? BoundaryCaptureSessionId,
    int? BoundaryCaptureSessionVersion,
    string? BoundaryCaptureSessionStatus,
    Guid? BaselineRoutePlanId,
    int? BaselineRoutePlanVersion,
    string? BaselineRoutePlanStatus,
    bool IsBaselineRouteCurrent,
    bool IsBaselineRouteWithinSafetyInset,
    bool HasPendingAdjustment,
    bool HasPendingSettlement,
    bool HasUnapprovedExtraFlight,
    bool HasValidWrittenDataUseConsent,
    Guid? WrittenDataUseConsentId,
    bool HasValidFlightPermission,
    Guid? FlightPermissionId,
    IReadOnlyList<OperationalAdjustmentDetail> Adjustments,
    IReadOnlyList<OperationalSettlementDetail> Settlements,
    IReadOnlyList<OperationalExtraFlightDetail> ExtraFlights,
    CommercialReadinessEvaluation CommercialReadiness,
    OperationalMissionReadiness MissionReadiness,
    DateTimeOffset EvaluatedAt,
    string? DepositPaymentStatus = null,
    decimal? DepositAmount = null,
    decimal? RemainingAmount = null,
    Guid? DepositPaymentId = null,
    DateTimeOffset? DepositConfirmedAt = null,
    double? CandidateCenterLongitude = null,
    double? CandidateCenterLatitude = null,
    decimal? ApproximateAreaHa = null,
    double? SurveyRadiusMeters = null);
