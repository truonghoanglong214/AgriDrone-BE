using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;

public sealed record ChangeDroneStatusResponse(
    Guid DroneId,
    DroneStatus Status,
    DateTimeOffset? LastMaintenanceAt,
    DateTimeOffset? NextMaintenanceAt,
    DateTimeOffset UpdatedAt);