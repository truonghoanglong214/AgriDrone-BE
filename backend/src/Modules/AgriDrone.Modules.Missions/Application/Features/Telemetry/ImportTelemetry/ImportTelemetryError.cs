using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

internal static class ImportTelemetryError
{
    public static AppError MissionStatusNotAllowed(
        MissionStatus status) =>
        AppError.Conflict(
            "TelemetryImport.MissionStatusNotAllowed",
            $"Mission in status '{status}' cannot import telemetry.");

    public static AppError OperationPayloadConflict(
        Guid operationId) =>
        AppError.Conflict(
            "TelemetryImport.OperationPayloadConflict",
            $"Operation '{operationId}' already exists " +
            "with different telemetry information.");

    public static AppError MissionAlreadyImported(
        Guid existingOperationId) =>
        AppError.Conflict(
            "TelemetryImport.MissionAlreadyImported",
            "The Mission already has telemetry imported by " +
            $"operation '{existingOperationId}'.");

    public static AppError FlightTimelineMissing() =>
        AppError.Conflict(
            "TelemetryImport.FlightTimelineMissing",
            "Mission start and end timestamps are required " +
            "before telemetry can be imported.");

    public static AppError TimestampOutsideMission() =>
        AppError.Validation(
            "TelemetryImport.TimestampOutsideMission",
            "Telemetry timestamps must be within the " +
            "Mission flight interval.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "TelemetryImport.ConcurrentUpdate",
            "The Mission or telemetry import changed concurrently. " +
            "Reload the Mission and retry.");
}