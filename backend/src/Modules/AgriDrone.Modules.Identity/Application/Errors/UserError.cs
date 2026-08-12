using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Errors
{
    public static class UserError
    {
        public static AppError NotFound(Guid id) =>
            AppError.NotFound(
                "User.NotFound",
                $"User with ID '{id}' was not found.");

        public static AppError EmailAlreadyExists(string email) =>
            AppError.Conflict(
                "User.EmailAlreadyExists",
                $"User with email '{email}' already exists.");
    }
}
