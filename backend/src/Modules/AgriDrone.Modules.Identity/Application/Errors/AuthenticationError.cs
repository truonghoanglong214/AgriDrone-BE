using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class AuthenticationError
{
    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "User.ContextRequired",
            "A valid user context is required.");

    public static AppError CurrentTenantRequired() =>
        AppError.Unauthorized(
            "Tenant.ContextRequired",
            "A valid tenant context is required.");

    public static AppError InvalidCredentials() =>
        AppError.Unauthorized(
            "User.InvalidCredentials",
            "Invalid email or password.");

    public static AppError InvalidTenantSelectionToken() =>
        AppError.Unauthorized(
            "Authentication.InvalidTenantSelectionToken",
            "The tenant selection token is invalid or expired.");

    public static AppError InvalidRefreshSession() =>
        AppError.Unauthorized(
            "Authentication.InvalidRefreshSession",
            "The refresh session is missing, expired, or no longer valid.");

    public static AppError RefreshConflict() =>
        AppError.Conflict(
            "Authentication.RefreshConflict",
            "The refresh session was rotated concurrently. Retry with the latest session cookie.");

    public static AppError InvalidOrigin() =>
        AppError.Forbidden(
            "Authentication.InvalidOrigin",
            "Request origin is not permitted for cookie authentication.");

    public static AppError SessionHeaderRequired() =>
        AppError.Forbidden(
            "Authentication.SessionHeaderRequired",
            "The X-AgriDrone-Session: 1 header is required for cookie authentication.");
}
