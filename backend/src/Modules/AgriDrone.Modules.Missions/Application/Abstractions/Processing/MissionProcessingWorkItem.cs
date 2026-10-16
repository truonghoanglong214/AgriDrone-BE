namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public sealed record MissionProcessingWorkItem(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId);
