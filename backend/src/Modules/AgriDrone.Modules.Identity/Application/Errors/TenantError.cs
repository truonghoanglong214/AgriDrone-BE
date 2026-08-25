using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Identity.Application.Errors;

public static class TenantError
{
    public static AppError CodeAlreadyExists(string tenantCode) =>
        AppError.Conflict(
            "Tenant.TenantCodeAlreadyExist",
            $"Tenant with Tenant Code '{tenantCode}' already exists.");

    public static AppError NotFound() =>
        AppError.NotFound(
            "Tenant.NotFound",
            "Tenant was not found.");

    public static AppError AccessDenied() =>
        AppError.Forbidden(
            "Tenant.AccessDenied",
            "The user does not have an active membership in the selected tenant.");

    public static AppError ContextRequired() =>
        AppError.Unauthorized(
            "Tenant.ContextRequired",
            "A valid tenant context is required.");
}
