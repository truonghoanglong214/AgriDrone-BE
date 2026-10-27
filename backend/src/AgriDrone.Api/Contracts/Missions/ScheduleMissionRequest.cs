namespace AgriDrone.Api.Contracts.Missions;

public sealed record ScheduleMissionRequest(
    DateOnly ScheduledDate,
    uint ExpectedVersion);
