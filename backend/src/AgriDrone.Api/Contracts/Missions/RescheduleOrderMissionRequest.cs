namespace AgriDrone.Api.Contracts.Missions;

public sealed record RescheduleOrderMissionRequest(
    uint ExpectedVersion, DateTimeOffset StartAt, DateTimeOffset EndAt,
    Guid? ReplacementDroneId = null);
