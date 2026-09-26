namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyOrderReadinessFailure
{
    ScopeNotConfirmed,
    AppointmentNotConfirmed,
    PaymentNotConfirmed,
    PrimaryManagerNotReady,
    PriceAdjustmentPending,
    OrderNotEligible
}
