using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Errors
{
    public static class UserError
    {
        public static AppError CurrentUserIsRequired() =>
            AppError.Unauthorized(
                "User.ContextRequired",
                "A valid tenant context is required.");

        public static AppError NotFound(string element, Guid id) =>
            AppError.NotFound(
                $"{element}.NotFound",
                $"{element} with ID '{id}' was not found.");

        public static AppError EmailAlreadyExists(string email) =>
            AppError.Conflict(
                "User.EmailAlreadyExists",
                $"User with email '{email}' already exists.");

        public static AppError TenantAlreadyExist(string tenantCode) =>
            AppError.Conflict(
                "Tenant.TenantCodeAlreadyExist",
                $"Tenant with Tenant Code '{tenantCode}' already exists.");

        public static AppError InvalidCredentials() =>
            AppError.Validation(
                "User.InvalidCredentials",
                "Invalid email or password.");

        public static AppError UserNotInAnyTenant(string email) =>
            AppError.Forbidden(
                "User.NotInAnyTenant",
                $"User with email '{email}' is not a member of any tenant.");

        public static AppError PasswordIsNotCorrect() =>
            AppError.Forbidden(
                "Password.NotCorrect",
                $"Old password is not correct.");

        public static AppError TenantNotFound() =>
            AppError.NotFound(
                "Tenant.NotFound",
                $"Tenant was not found.");

        public static AppError UserNotFound() =>
            AppError.NotFound(
                "User.NotFound",
                $"User was not found.");

        public static AppError InvalidTenantSelectionToken() =>
            AppError.Unauthorized(
                "Authentication.InvalidTenantSelectionToken",
                "The tenant selection token is invalid or expired.");

        public static AppError TenantAccessDenied() =>
            AppError.Forbidden(
                "Tenant.AccessDenied",
                "The user does not have an active membership in the selected tenant.");

        public static AppError CurrentTenantRequired() =>
            AppError.Unauthorized(
                "Tenant.ContextRequired",
                "A valid tenant context is required.");
    }
}
