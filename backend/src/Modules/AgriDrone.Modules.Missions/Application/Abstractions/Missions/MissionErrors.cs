using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal static class MissionError
{
    public static AppError CurrentTenantRequired() =>
        AppError.Unauthorized(
            "Mission.TenantContextRequired",
            "A valid tenant context is required.");

    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "Mission.UserContextRequired",
            "A valid user context is required.");

    public static AppError NotFound(Guid missionId) =>
        AppError.NotFound(
            "Mission.NotFound",
            $"Mission with ID '{missionId}' was not found.");

    public static AppError DroneNotFound(Guid droneId) =>
        AppError.NotFound(
            "Mission.DroneNotFound",
            $"Drone with ID '{droneId}' was not found.");

    public static AppError ZoneNotActive(Guid zoneId) =>
        AppError.Validation(
            "Mission.ZoneNotActive",
            $"Zone '{zoneId}' does not exist in the farm or is not active.");

    public static AppError SourceMapNotConfirmed(Guid mapVersionId) =>
        AppError.Validation(
            "Mission.SourceMapNotConfirmed",
            $"Map version '{mapVersionId}' is not the confirmed map for the selected zone.");

    public static AppError CodeAlreadyExists(string code) =>
        AppError.Conflict(
            "Mission.CodeAlreadyExists",
            $"Mission code '{code}' already exists in the farm.");

    public static AppError DroneNotAvailable(Guid droneId) =>
        AppError.Conflict(
            "Mission.DroneNotAvailable",
            $"Drone '{droneId}' is not available for the requested period.");

    public static AppError VersionConflict(
        uint expectedVersion,
        uint currentVersion) =>
        AppError.Conflict(
            "Mission.VersionConflict",
            $"Expected mission version '{expectedVersion}', " +
            $"but current version is '{currentVersion}'.");

    public static AppError InvalidTransition(
        MissionStatus currentStatus,
        MissionStatus targetStatus) =>
        AppError.Conflict(
            "Mission.InvalidTransition",
            $"Mission cannot change from '{currentStatus}' " +
            $"to '{targetStatus}'.");

    public static AppError InvalidSchedule() =>
        AppError.Validation(
            "Mission.InvalidSchedule",
            "Scheduled end time must be later than start time.");

    public static AppError ReasonRequired(
        MissionStatus targetStatus) =>
        AppError.Validation(
            "Mission.ReasonRequired",
            $"A reason is required when changing to '{targetStatus}'.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "Mission.ConcurrentUpdate",
            "The mission was changed by another request. " +
            "Reload the mission and try again.");

    public static AppError ScheduleConflict(Guid droneId) =>
        AppError.Conflict(
            "Mission.ScheduleConflict",
            $"Drone '{droneId}' has another overlapping mission.");
}
