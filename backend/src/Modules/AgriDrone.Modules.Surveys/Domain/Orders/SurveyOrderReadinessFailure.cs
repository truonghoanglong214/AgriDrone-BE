namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyOrderReadinessFailure
{
    BaselineMappingNotRequired,
    BoundaryNotApproved,
    ScopeNotConfirmed,
    AppointmentNotConfirmed,
    AppointmentPurposeMismatch,
    FarmBaseMapNotPublished,
    PoleCountNotConfirmed,
    PriceNotConfirmed,
    PaymentNotConfirmed,
    PrimaryManagerNotReady,
    PriceAdjustmentPending,
    SafetyChecksNotSatisfied,
    OrderNotEligible
}
