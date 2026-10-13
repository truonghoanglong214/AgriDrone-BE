using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

internal static class PrepareMissionSetErrors
{
    public static AppError OrderContextUnavailable() =>
        AppError.Failure(
            "MissionPlanning.OrderContextUnavailable",
            "The authoritative Survey Order planning context is unavailable. Retry later.");

    public static AppError OrderNotFound(Guid orderId) =>
        AppError.NotFound(
            "MissionPlanning.OrderNotFound",
            $"Survey Order '{orderId}' was not found.");

    public static AppError OrderNotReady(string? reason) =>
        AppError.Conflict(
            "MissionPlanning.OrderNotReady",
            string.IsNullOrWhiteSpace(reason)
                ? "The Survey Order is not eligible for mission planning."
                : $"The Survey Order is not eligible for mission planning: {reason}.");

    public static AppError FarmAccessDenied() =>
        AppError.Forbidden(
            "MissionPlanning.FarmAccessDenied",
            "The current SystemManager is not allowed to operate this Farm.");

    public static AppError CrossTenantOrder() =>
        AppError.Forbidden(
            "MissionPlanning.CrossTenantOrder",
            "The Survey Order does not belong to the assigned Farm tenant.");

    public static AppError InvalidOrderContext() =>
        AppError.Failure(
            "MissionPlanning.InvalidOrderContext",
            "The authoritative Survey Order planning context is inconsistent.");

    public static AppError BaselineWindowRequired() =>
        AppError.Validation(
            "MissionPlanning.BaselineWindowRequired",
            "A baseline schedule window is required to schedule the confirmed baseline appointment.");

    public static AppError ServiceWindowRequired() =>
        AppError.Validation(
            "MissionPlanning.ServiceWindowRequired",
            "A service schedule window is required to schedule the confirmed paid-service appointment.");

    public static AppError ServiceWindowNotAllowed() =>
        AppError.Validation(
            "MissionPlanning.ServiceWindowNotAllowed",
            "A paid-service schedule window is not accepted while baseline mapping is required.");

    public static AppError BaselineWindowNotAllowed() =>
        AppError.Validation(
            "MissionPlanning.BaselineWindowNotAllowed",
            "A baseline schedule window is not allowed when the Farm already has an approved base map.");

    public static AppError ScheduleOutsideAppointment() =>
        AppError.Validation(
            "MissionPlanning.ScheduleOutsideAppointment",
            "Every Mission schedule must be inside the confirmed appointment window.");

    public static AppError ScheduleOverlap() =>
        AppError.Validation(
            "MissionPlanning.ScheduleOverlap",
            "Baseline and service Mission schedules cannot overlap.");

    public static AppError BaselineMustPrecedeService() =>
        AppError.Validation(
            "MissionPlanning.BaselineMustPrecedeService",
            "The baseline Mission must be scheduled before the service Mission.");

    public static AppError DroneNotAvailable(Guid droneId) =>
        AppError.Conflict(
            "MissionPlanning.DroneNotAvailable",
            $"Drone '{droneId}' is not available for every requested schedule window.");

    public static AppError ConcurrentPreparation() =>
        AppError.Conflict(
            "MissionPlanning.ConcurrentPreparation",
            "The Mission set was prepared concurrently. Retry the same request to receive the existing set.");

    public static AppError ExistingMissionSetRequiresRecovery() =>
        AppError.Conflict(
            "MissionPlanning.ExistingMissionSetRequiresRecovery",
            "The existing Mission set is inconsistent, failed or cancelled and cannot be prepared again.");
}
