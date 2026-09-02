namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class MissionCodeConflictException(
    Exception innerException)
    : Exception(
        "A mission with the same farm code already exists.",
        innerException);
