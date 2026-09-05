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
}
