namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyOrderReadinessFailure
{
    BaselineMappingNotRequired,
    BoundaryNotApproved,
    ScopeNotConfirmed,
    AppointmentNotConfirmed,
    AppointmentPurposeMismatch,
    AppointmentNotCurrent,
    MissionNotPreparedForPurpose,
    MissionScheduleMismatch,
    DroneNotReady,
    PreflightNotCompleted,
    FarmBaseMapNotPublished,
    PoleCountNotConfirmed,
    PriceNotConfirmed,
    PaymentNotConfirmed,
    PaymentAmountOrCurrencyMismatch,
    PaymentRevoked,
    PrimaryManagerNotReady,
    PrimaryManagerUnavailable,
    PriceAdjustmentPending,
    SafetyChecksNotSatisfied,
    OrderNotEligible,
    ServiceDisabled,
    FinancialSettlementPending,
    ExtraFlightNotApproved,
    BoundaryCaptureNotFinalized,
    BaselineRouteNotApproved,
    BaselineRouteStale,
    BaselineRouteOutsideSafetyInset
}
