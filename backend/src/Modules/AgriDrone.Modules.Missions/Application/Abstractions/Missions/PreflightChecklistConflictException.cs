namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal sealed class PreflightChecklistConflictException(Exception innerException)
    : Exception("A preflight checklist version or completion already exists.", innerException);
