namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class MissionConcurrencyException(
    Exception innerException)
    : Exception(
        "The mission was changed by another request.",
        innerException);