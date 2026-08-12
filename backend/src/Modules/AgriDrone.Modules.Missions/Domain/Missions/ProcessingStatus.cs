namespace AgriDrone.Modules.Missions.Domain.Missions;

public enum ProcessingStatus
{
    NotUploaded,
    Uploaded,
    Queued,
    Processing,
    Completed,
    Failed,
    ReviewRequired
}
