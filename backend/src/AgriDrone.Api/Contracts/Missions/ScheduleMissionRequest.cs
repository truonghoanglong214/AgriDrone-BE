namespace AgriDrone.Api.Contracts.Missions;

public sealed record ScheduleMissionRequest(
    DateTimeOffset ScheduledAt,
    DateTimeOffset ScheduledEndAt,
    uint ExpectedVersion);