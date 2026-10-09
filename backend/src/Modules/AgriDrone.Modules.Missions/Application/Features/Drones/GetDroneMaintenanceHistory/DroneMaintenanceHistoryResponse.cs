using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneMaintenanceHistory;

public sealed record DroneMaintenanceHistoryResponse(
    Guid Id,
    Guid DroneId,
    DateTimeOffset StartedAt,
    Guid? StartedBy,
    string? Reason,
    DateTimeOffset? ClosedAt,
    Guid? ClosedBy,
    DroneStatus? ClosingStatus,
    DateTimeOffset? NextMaintenanceAt);
