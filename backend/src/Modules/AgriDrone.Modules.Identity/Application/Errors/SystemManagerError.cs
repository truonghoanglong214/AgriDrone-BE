using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class SystemManagerError
{
    public static AppError ProfileNotFound() =>
        AppError.NotFound(
            "SystemManager.ProfileNotFound",
            "The SystemManager profile was not found.");

    public static AppError ProfileAlreadyExists() =>
        AppError.Conflict(
            "SystemManager.ProfileAlreadyExists",
            "The user already has a SystemManager profile.");

    public static AppError UserMustBeActive() =>
        AppError.Conflict(
            "SystemManager.UserMustBeActive",
            "A SystemManager profile can only be created for an active user.");

    public static AppError RoleMissing() =>
        AppError.Failure(
            "SystemManager.RoleMissing",
            "The SYSTEM_MANAGER role has not been seeded.");

    public static AppError NotAssignable() =>
        AppError.Conflict(
            "SystemManager.NotAssignable",
            "The manager must have an active profile, be available, and hold a non-expired flight qualification.");

    public static AppError InvalidQualificationExpiry() =>
        AppError.Validation(
            "SystemManager.InvalidQualificationExpiry",
            "A qualified manager requires a qualification expiry in the future.");

    public static AppError FarmNotFound() =>
        AppError.NotFound(
            "SystemManager.FarmNotFound",
            "The active Farm was not found.");

    public static AppError AssignmentNotFound() =>
        AppError.NotFound(
            "SystemManager.AssignmentNotFound",
            "The Farm has no active primary SystemManager assignment.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "SystemManager.ConcurrentUpdate",
            "The profile or assignment changed while the request was being processed. Reload it and try again.");

    public static AppError ActiveAssignmentConflict() =>
        AppError.Conflict(
            "SystemManager.ActiveAssignmentConflict",
            "The Farm already has another active primary SystemManager assignment.");
}
