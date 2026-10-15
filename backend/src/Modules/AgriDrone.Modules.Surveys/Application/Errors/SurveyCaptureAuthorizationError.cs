using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyCaptureAuthorizationError
{
    public static AppError NotFound() => AppError.NotFound(
        "SurveyCaptureAuthorization.NotFound",
        "The capture-authorization requirement was not found.");
    public static AppError Forbidden() => AppError.Forbidden(
        "SurveyCaptureAuthorization.Forbidden",
        "The actor cannot change capture authorization.");
    public static AppError Conflict(string description) => AppError.Conflict(
        "SurveyCaptureAuthorization.Conflict",
        description);
}
