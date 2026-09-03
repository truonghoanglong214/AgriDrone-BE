namespace AgriDrone.Api.Contracts.Drones;

public sealed record GetAvailableDronesRequest(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);