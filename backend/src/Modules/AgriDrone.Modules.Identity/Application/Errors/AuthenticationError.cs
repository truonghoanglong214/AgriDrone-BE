using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class AuthenticationError
{
    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "User.ContextRequired",
            "A valid user context is required.");

    public static AppError InvalidCredentials() =>
        AppError.Validation(
            "User.InvalidCredentials",
            "Invalid email or password.");

    public static AppError InvalidTenantSelectionToken() =>
        AppError.Unauthorized(
            "Authentication.InvalidTenantSelectionToken",
            "The tenant selection token is invalid or expired.");
}
