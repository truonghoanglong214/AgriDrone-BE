using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;

internal static class FinalizeMissionUploadError
{
    public static AppError MissionStatusNotAllowed(
        MissionStatus status) =>
        AppError.Conflict(
            "MissionUpload.StatusNotAllowed",
            $"Mission in status '{status}' cannot be finalized.");

    public static AppError ActiveUploadSessionsExist() =>
        AppError.Conflict(
            "MissionUpload.ActiveSessionsExist",
            "Mission still has pending or verifying upload sessions.");

    public static AppError RequiredMediaMissing(
        MissionType missionType) =>
        AppError.Conflict(
            "MissionUpload.RequiredMediaMissing",
            $"Mission type '{missionType}' does not have " +
            "the required verified input media.");

    public static AppError TelemetryMissing() =>
        AppError.Conflict(
            "MissionUpload.TelemetryMissing",
            "Mission does not have a completed telemetry import.");

    public static AppError TelemetryInconsistent(
        int importedPointCount,
        int persistedPointCount) =>
        AppError.Conflict(
            "MissionUpload.TelemetryInconsistent",
            $"Telemetry import declares {importedPointCount} points " +
            $"but PostgreSQL contains {persistedPointCount} points.");

    public static AppError FlightRouteMissing() =>
        AppError.Conflict(
            "MissionUpload.FlightRouteMissing",
            "Mission does not have a valid actual flight route.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "MissionUpload.ConcurrentUpdate",
            "Mission changed concurrently. Reload it and retry.");
}