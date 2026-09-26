namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyOrderStatus
{
    PendingScopeConfirmation,
    AwaitingAppointment,
    AwaitingPayment,
    ReadyForOperations,
    InProgress,
    PendingReview,
    Completed,
    Cancelled
}
