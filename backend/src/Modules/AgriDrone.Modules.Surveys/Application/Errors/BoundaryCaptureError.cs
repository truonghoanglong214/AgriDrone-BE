using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class BoundaryCaptureError
{
    public static AppError NotFound(Guid id) => AppError.NotFound(
        "BoundaryCapture.NotFound",
        $"Boundary capture session '{id}' was not found.");

    public static AppError Forbidden() => AppError.Forbidden(
        "BoundaryCapture.Forbidden",
        "Only the currently assigned, qualified SystemManager can mutate this capture session.");

    public static AppError InvalidState(string description) => AppError.Conflict(
        "BoundaryCapture.InvalidState",
        description);

    public static AppError AppointmentNotConfirmed() => AppError.Validation(
        "BoundaryCapture.AppointmentNotConfirmed",
        "A current confirmed Baseline appointment is required.");

    public static AppError AppointmentWindowClosed() => AppError.Validation(
        "BoundaryCapture.AppointmentWindowClosed",
        "Boundary capture can only start within the confirmed appointment window.");

    public static AppError AuthorizationNotValid() => AppError.Validation(
        "BoundaryCapture.AuthorizationNotValid",
        "Valid written data-use consent and flight permission are required.");

    public static AppError DroneNotAvailable() => AppError.Validation(
        "BoundaryCapture.DroneNotAvailable",
        "The selected drone is not available for boundary capture.");

    public static AppError InvalidPolygon(string description) => AppError.Validation(
        "BoundaryCapture.InvalidPolygon",
        description);

    public static AppError PersistenceConflict() => AppError.Conflict(
        "BoundaryCapture.PersistenceConflict",
        "The capture workflow was changed concurrently. Reload and retry.");
}

public static class BaselineRouteError
{
    public static AppError StaleReference() => AppError.Conflict(
        "BaselineRoute.StaleReference",
        "The route does not reference the current finalized capture session and boundary version.");

    public static AppError OutsideSafetyInset() => AppError.Validation(
        "BaselineRoute.OutsideSafetyInset",
        "The route cannot be approved because it is outside the required safety inset.");
}
