namespace AgriDrone.Modules.Missions.Domain.Media;

public enum MediaUploadSessionStatus
{
    Pending,
    Verifying,
    Completed,
    Failed,
    Expired,
    Aborted
}