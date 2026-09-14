using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;

public sealed record DroneListItemResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string? Model,
    string? Manufacturer,
    string? SerialNumber,
    DroneStatus Status,
    DateTimeOffset? NextMaintenanceAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);