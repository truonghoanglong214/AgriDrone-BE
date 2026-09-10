using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Errors;

internal static class DroneError
{
    public static AppError CurrentTenantRequired() =>
        AppError.Unauthorized(
            "Drone.TenantContextRequired",
            "A valid tenant context is required.");

    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "Drone.UserContextRequired",
            "A valid user context is required.");

    public static AppError NotFound(Guid droneId) =>
        AppError.NotFound(
            "Drone.NotFound",
            $"Drone with ID '{droneId}' was not found.");

    public static AppError CodeAlreadyExists(string code) =>
        AppError.Conflict(
            "Drone.CodeAlreadyExists",
            $"Drone code '{code}' already exists.");

    public static AppError SerialNumberAlreadyExists(
        string serialNumber) =>
        AppError.Conflict(
            "Drone.SerialNumberAlreadyExists",
            $"Drone serial number '{serialNumber}' already exists.");

    public static AppError RegistrationNumberAlreadyExists(
        string registrationNumber) =>
        AppError.Conflict(
            "Drone.RegistrationNumberAlreadyExists",
            $"Drone registration number '{registrationNumber}' already exists.");

    public static AppError InvalidStatusTransition(
        DroneStatus currentStatus,
        DroneStatus targetStatus) =>
        AppError.Conflict(
            "Drone.InvalidStatusTransition",
            $"Drone cannot change from '{currentStatus}' to '{targetStatus}'.");

    public static AppError NextMaintenanceNotAllowed() =>
    AppError.Validation(
        "Drone.NextMaintenanceNotAllowed",
        "Next maintenance time can only be provided when completing maintenance.");

    public static AppError InvalidNextMaintenanceTime() =>
        AppError.Validation(
            "Drone.InvalidNextMaintenanceTime",
            "Next maintenance time must be later than maintenance completion time.");

    public static AppError HasBlockingMission(Guid droneId) =>
        AppError.Conflict(
            "Drone.HasBlockingMission",
            $"Drone with ID '{droneId}' is assigned " +
            "to a draft, scheduled or in-flight mission.");
}