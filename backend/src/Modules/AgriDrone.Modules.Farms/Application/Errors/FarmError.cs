using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Errors
{
    public static class FarmError
    {
        public static AppError CodeAlreadyExists(string farmCode) =>
        AppError.Conflict(
            "Farm.FarmCodeAlreadyExist",
            $"Farm with Farm Code '{farmCode}' already exists.");

        public static AppError NotFound() =>
            AppError.NotFound(
                "Farm.NotFound",
                "Farm was not found.");

        public static AppError AccessDenied() =>
            AppError.Forbidden(
                "Farm.AccessDenied",
                "The user does not have the required access to the selected farm.");

        public static AppError FarmNotFound(Guid farmId) =>
            AppError.Validation(
                "Farm.FarmNotFound",
                $"Farm with ID '{farmId}' was not found.");

        public static AppError ConcurrentUpdate() =>
            AppError.Conflict(
                "Farm.ConcurrentUpdate",
                "The farm was changed by another request. Reload it and try again.");
    }
}
