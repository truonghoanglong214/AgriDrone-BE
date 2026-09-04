namespace AgriDrone.Modules.Missions.Domain.Missions;

public enum MissionStatus
{
    Draft,
    Scheduled,
    InFlight,
    FlightCompleted,
    Uploading,
    ReadyForProcessing,
    Processing,
    AwaitingReview,
    Completed,
    Cancelled,
    FlightFailed,
    UploadFailed,
    ProcessingFailed
}