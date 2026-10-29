namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public sealed record MissionPlantGpsPublishResult(
    MissionPlantGpsPublishStatus Status,
    Guid? MessageId = null,
    IReadOnlyList<string>? Errors = null);
