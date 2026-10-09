namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class MissionSetConflictException(
    Exception innerException)
    : Exception(
        "The Survey Order already has a Mission with the same purpose.",
        innerException);
