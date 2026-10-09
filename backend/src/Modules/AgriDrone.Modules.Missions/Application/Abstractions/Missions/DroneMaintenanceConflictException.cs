namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class DroneMaintenanceConflictException(Exception innerException)
    : Exception("Drone maintenance state changed concurrently.", innerException);
