namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyOrderStatus
{
    PendingBoundaryVerification,
    AwaitingBaselineAppointment,
    AwaitingBaselineDeposit,
    AwaitingBoundaryCapture,
    BaselineReady,
    BaselineInProgress,
    AwaitingBaselineReview,
    AwaitingPricing,
    AwaitingPaidAppointment,
    AwaitingPayment,
    ReadyForPaidService,
    InProgress,
    PendingReview,
    Completed,
    Cancelled,

    // Additive V3 statuses
    AwaitingAppointment,
    AwaitingDeposit,
    ReadyForCapture,
    Capturing,
    AwaitingCaptureUpload,
    AwaitingInputReview,
    AwaitingProcessing,
    Processing,
    AwaitingReflightDecision,
    AwaitingSettlement
}
