namespace AgriDrone.Api.Contracts.Missions;

public sealed record ProcessMetashapeRequest(
    string? CameraType = null,
    string? AlignAccuracy = null,
    bool? GenericPreselection = null);

public sealed record ProcessMetashapeResponse(
    Guid MissionId,
    string TaskId,
    string Status,
    string Message);
