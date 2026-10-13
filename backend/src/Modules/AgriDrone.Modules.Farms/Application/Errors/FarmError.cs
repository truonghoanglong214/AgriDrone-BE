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

        public static AppError InvalidBoundary() => AppError.Validation(
            "Farm.InvalidBoundary",
            "The farm boundary must be a non-empty valid polygon using SRID 4326.");

        public static AppError InvalidCenterPoint() => AppError.Validation(
            "Farm.InvalidCenterPoint",
            "The farm center point must use SRID 4326 and valid coordinates.");

        public static AppError CenterPointOutsideBoundary() => AppError.Validation(
            "Farm.CenterPointOutsideBoundary",
            "The farm center point must be covered by the farm boundary.");

        public static AppError InvalidArea() => AppError.Validation(
            "Farm.InvalidArea",
            "The farm area must be greater than or equal to zero.");

        public static AppError FarmNotFound(Guid farmId) =>
            AppError.Validation(
                "Farm.FarmNotFound",
                $"Farm with ID '{farmId}' was not found.");

        public static AppError ConcurrentUpdate() =>
            AppError.Conflict(
                "Farm.ConcurrentUpdate",
                "The farm was changed by another request. Reload it and try again.");

        public static AppError ActiveDependenciesExist(
            int activeZoneCount,
            int activeMissionCount) =>
            AppError.Conflict(
                "Farm.ActiveDependenciesExist",
                "The farm cannot be archived while active dependencies remain. " +
                $"Active zones: {activeZoneCount}; active missions: {activeMissionCount}.");
    }
}
