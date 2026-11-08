namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

public enum MissionPlantHealthPublishStatus
{
    Queued,
    NotFound,
    Conflict,
    Invalid
}

public sealed record MissionPlantHealthPublishResult(
    MissionPlantHealthPublishStatus Status,
    Guid? MessageId = null,
    IReadOnlyList<string>? Errors = null);

