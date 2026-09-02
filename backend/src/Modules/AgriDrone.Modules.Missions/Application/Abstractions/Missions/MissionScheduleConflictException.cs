namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class MissionScheduleConflictException(
    Exception innerException)
    : Exception(
        "The drone has another overlapping mission.",
        innerException);