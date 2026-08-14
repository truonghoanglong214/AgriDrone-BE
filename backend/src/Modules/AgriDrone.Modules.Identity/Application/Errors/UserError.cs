using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Errors
{
    public static class UserError
    {
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
    }
}
