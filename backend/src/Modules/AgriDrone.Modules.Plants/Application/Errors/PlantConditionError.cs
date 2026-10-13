using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Plants.Application.Errors
{
    public static class PlantConditionError
    {
        public static AppError NotFound() =>
            AppError.NotFound(
                "PlantCondition.NotFound",
                "Plant condition was not found.");

        public static AppError CodeAlreadyExists(string code) =>
            AppError.Conflict(
                "PlantCondition.CodeAlreadyExists",
                $"Plant condition code '{code}' already exists.");

        public static AppError AlreadyRetired() =>
            AppError.Conflict(
                "PlantCondition.AlreadyRetired",
                "The plant condition has already been retired.");

        public static AppError ConcurrentUpdate() =>
            AppError.Conflict(
                "PlantCondition.ConcurrentUpdate",
                "The plant condition was changed by another request. Reload it and try again.");

        public static AppError CurrentUserRequired() =>
            AppError.Unauthorized(
                "PlantCondition.CurrentUserRequired",
                "An authenticated system administrator is required.");
    }
}
