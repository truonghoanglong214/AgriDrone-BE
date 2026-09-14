using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Api.Contracts.Drones;

public sealed record GetSystemDronesRequest
{
    public Guid? TenantId { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public DroneStatus? Status { get; init; }

    public string? Search { get; init; }
}