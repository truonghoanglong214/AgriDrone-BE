using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;

public sealed record MissionListItemResponse(
    Guid Id,
    Guid FarmId,
    Guid ZoneId,
    Guid DroneId,
    string MissionCode,
    MissionType MissionType,
    MissionStatus Status,
    ProcessingStatus ProcessingStatus,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ScheduledEndAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    uint Version,
    DateTimeOffset CreatedAt);
