namespace AgriDrone.Modules.Surveys.Domain;

public enum CaptureAttemptStatus
{
    Planned,
    PreflightReady,
    InFlight,
    AwaitingUpload,
    Uploaded,
    Processing,
    Accepted,
    ReflightRequired,
    FlightFailed,
    Cancelled
}
