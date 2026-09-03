using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Errors
{
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
    }
}
