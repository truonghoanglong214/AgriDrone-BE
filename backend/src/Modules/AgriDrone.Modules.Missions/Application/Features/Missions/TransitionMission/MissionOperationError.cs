using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;

internal static class MissionOperationError
{
    public static AppError FarmAccessDenied() =>
        AppError.Forbidden("MissionOperation.FarmAccessDenied",
            "The current SystemManager is not assigned and flight-qualified for this Farm.");

    public static AppError MissionNotOrderBound() =>
        AppError.Conflict("MissionOperation.MissionNotOrderBound",
            "This operational endpoint only accepts order-bound Missions.");

    public static AppError OrderContextUnavailable() =>
        AppError.Failure("MissionOperation.OrderContextUnavailable",
            "The authoritative Survey Order context is unavailable. Retry later.");

    public static AppError OrderNotFound(Guid orderId) =>
        AppError.NotFound("MissionOperation.OrderNotFound",
            $"Survey Order '{orderId}' was not found.");

    public static AppError InvalidOrderContext() =>
        AppError.Failure("MissionOperation.InvalidOrderContext",
            "The authoritative Survey Order context is inconsistent with the Mission.");

    public static AppError OrderNotReady(string? reason) =>
        AppError.Conflict("MissionOperation.OrderNotReady",
            string.IsNullOrWhiteSpace(reason)
                ? "The Survey Order is not ready for flight operations."
                : $"The Survey Order is not ready for flight operations: {reason}.");

    public static AppError ScheduleWindowClosed() =>
        AppError.Conflict("MissionOperation.ScheduleWindowClosed",
            "The current time is outside the Mission schedule window.");

    public static AppError AppointmentWindowClosed() =>
        AppError.Conflict("MissionOperation.AppointmentWindowClosed",
            "The current time is outside the confirmed Survey Order appointment window.");

    public static AppError PreflightRequired() =>
        AppError.Conflict("MissionOperation.PreflightRequired",
            "A completed and suitable pre-flight checklist is required before flight.");

    public static AppError PreflightStale() =>
        AppError.Conflict("MissionOperation.PreflightStale",
            "The pre-flight checklist definition changed. Complete the current checklist before flight.");

    public static AppError DroneNotOperational(Guid droneId) =>
        AppError.Conflict("MissionOperation.DroneNotOperational",
            $"Drone '{droneId}' is unavailable, unregistered, expired or due for maintenance.");

    public static AppError BaselineNotCompleted() =>
        AppError.Conflict("MissionOperation.BaselineNotCompleted",
            "The required baseline map has not been completed for this service Mission.");
}
