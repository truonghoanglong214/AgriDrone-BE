namespace AgriDrone.Api.Contracts.Missions;

public sealed record RescheduleOrderMissionRequest(
    uint ExpectedVersion, DateOnly ScheduledDate,
    Guid? ReplacementDroneId = null);
