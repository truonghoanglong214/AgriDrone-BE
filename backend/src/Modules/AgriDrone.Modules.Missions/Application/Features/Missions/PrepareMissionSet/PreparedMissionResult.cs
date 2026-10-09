using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

public sealed record PreparedMissionResult(
    Guid MissionId,
    MissionPurpose Purpose,
    MissionStatus Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ScheduledEndAt,
    uint Version);
