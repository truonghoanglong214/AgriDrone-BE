using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Harvests.Application.Errors
{
    public static class HarvestQualityGradeError
    {
        public static AppError NotFound() =>
            AppError.NotFound(
                "HarvestQualityGrade.NotFound",
                "Harvest quality grade was not found.");

        public static AppError CodeAlreadyExists(string code) =>
            AppError.Conflict(
                "HarvestQualityGrade.CodeAlreadyExists",
                $"Harvest quality grade code '{code}' already exists.");

        public static AppError AlreadyRetired() =>
            AppError.Conflict(
                "HarvestQualityGrade.AlreadyRetired",
                "The harvest quality grade has already been retired.");

        public static AppError ConcurrentUpdate() =>
            AppError.Conflict(
                "HarvestQualityGrade.ConcurrentUpdate",
                "The harvest quality grade was changed by another request.");

        public static AppError CurrentUserRequired() =>
            AppError.Unauthorized(
                "HarvestQualityGrade.CurrentUserRequired",
                "An authenticated system administrator is required.");
    }
}
