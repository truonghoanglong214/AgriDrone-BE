namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class DroneConcurrencyException(Exception innerException)
    : Exception("The Drone was concurrently updated.", innerException);
