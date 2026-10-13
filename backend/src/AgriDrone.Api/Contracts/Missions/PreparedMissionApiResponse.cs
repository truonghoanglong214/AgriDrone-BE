namespace AgriDrone.Api.Contracts.Missions;

public sealed record PreparedMissionApiResponse(
    Guid MissionId,
    string Purpose,
    string Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? ScheduledEndAt,
    uint Version);
