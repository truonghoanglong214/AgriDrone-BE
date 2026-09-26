namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyPaymentStatus
{
    Pending,
    Processing,
    Confirmed,
    Failed,
    Refunded,
    AdjustmentRequired
}
