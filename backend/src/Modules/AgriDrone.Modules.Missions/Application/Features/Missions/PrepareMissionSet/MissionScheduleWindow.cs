namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

public sealed record MissionScheduleWindow(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);
