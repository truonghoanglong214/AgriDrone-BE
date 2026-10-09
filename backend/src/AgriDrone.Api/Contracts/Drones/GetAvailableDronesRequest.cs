namespace AgriDrone.Api.Contracts.Drones;

public sealed record GetAvailableDronesRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose? Purpose = null);
