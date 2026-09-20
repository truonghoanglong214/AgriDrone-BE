using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Plants.Application.Errors;

public static class PlantRegistrationError
{
    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "User.ContextRequired",
            "A valid user context is required.");

    public static AppError CurrentTenantRequired() =>
        AppError.Unauthorized(
            "Tenant.ContextRequired",
            "A valid tenant context is required.");

    public static AppError ZoneNotFound() =>
        AppError.NotFound(
            "PlantRegistration.ZoneNotFound",
            "The active farm zone was not found.");

    public static AppError AccessDenied() =>
        AppError.Forbidden(
            "PlantRegistration.AccessDenied",
            "Manager access to the selected farm zone is required.");

    public static AppError MapVersionNotFound() =>
        AppError.NotFound(
            "PlantRegistration.MapVersionNotFound",
            "The confirmed map version was not found in the selected farm zone.");

    public static AppError CodeAlreadyExists(string plantCode) =>
        AppError.Conflict(
            "PlantRegistration.CodeAlreadyExists",
            $"Plant code '{plantCode}' already exists in this farm.");

    public static AppError GridPositionOccupied(
        int rowIndex,
        int columnIndex) =>
        AppError.Conflict(
            "PlantRegistration.GridPositionOccupied",
            $"Grid position ({rowIndex}, {columnIndex}) is already occupied in this zone.");

    public static AppError UnknownHealthLevelMissing() =>
        AppError.Failure(
            "PlantRegistration.UnknownHealthLevelMissing",
            "The active UNKNOWN health level is not configured.");
}
