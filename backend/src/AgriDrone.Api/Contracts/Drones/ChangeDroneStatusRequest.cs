using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Api.Contracts.Drones;

public sealed record ChangeDroneStatusRequest(
    DroneStatus Status,
    DateTimeOffset? NextMaintenanceAt);