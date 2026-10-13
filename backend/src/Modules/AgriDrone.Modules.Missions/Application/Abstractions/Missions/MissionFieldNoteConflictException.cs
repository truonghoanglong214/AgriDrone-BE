namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public sealed class MissionFieldNoteConflictException(Exception innerException)
    : Exception("The field note operation already exists.", innerException);
