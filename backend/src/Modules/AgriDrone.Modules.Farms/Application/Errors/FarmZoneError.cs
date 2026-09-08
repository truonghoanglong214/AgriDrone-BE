using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Farms.Application.Errors;

public static class FarmZoneError
{
    public static AppError CodeAlreadyExists(string code) =>
        AppError.Conflict(
            "FarmZone.CodeAlreadyExists",
            $"A zone with code '{code}' already exists in this farm.");

    public static AppError NotFound() =>
        AppError.NotFound(
            "FarmZone.NotFound",
            "The farm zone was not found.");

    public static AppError AccessDenied() =>
        AppError.Forbidden(
            "FarmZone.AccessDenied",
            "The user does not have access to the selected farm zone.");

    public static AppError BoundaryOutsideFarm() =>
        AppError.Validation(
            "FarmZone.BoundaryOutsideFarm",
            "The zone boundary must be contained within the farm boundary.");

    public static AppError BoundaryOverlaps() =>
        AppError.Conflict(
            "FarmZone.BoundaryOverlaps",
            "The zone boundary overlaps another active zone in this farm.");

    public static AppError ConcurrentUpdate() =>
            AppError.Conflict(
                "FarmZone.ConcurrentUpdate",
                "The zone was changed by another request. Reload it and try again.");

    public static AppError ActiveDependenciesExist(
        int activeMissionCount,
        int openFieldTaskCount) =>
        AppError.Conflict(
            "FarmZone.ActiveDependenciesExist",
            "The zone cannot be archived while active dependencies remain. " +
            $"Active missions: {activeMissionCount}; " +
            $"open field tasks: {openFieldTaskCount}.");
}
