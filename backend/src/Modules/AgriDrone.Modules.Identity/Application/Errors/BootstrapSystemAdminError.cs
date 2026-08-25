using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

internal static class BootstrapSystemAdminError
{
    public static AppError SystemRoleMissing() =>
        AppError.Failure(
            "Bootstrap.SystemAdminRoleMissing",
            "The SYSTEM_ADMIN role was not found. Database migration and role seeding must run before bootstrap.");

    public static AppError ConfiguredEmailAlreadyExists(string email) =>
        AppError.Conflict(
            "Bootstrap.ConfiguredEmailAlreadyExists",
            $"The configured System Admin email '{email}' already belongs to a user. Automatic privilege elevation is not allowed.");
}
