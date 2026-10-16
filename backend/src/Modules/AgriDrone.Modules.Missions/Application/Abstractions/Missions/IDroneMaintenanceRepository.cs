using AgriDrone.Modules.Missions.Domain.Drones;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Missions;

internal interface IDroneMaintenanceRepository
{
    Task<DroneMaintenanceRecord?> GetOpenAsync(Guid droneId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DroneMaintenanceRecord>> ListAsync(Guid droneId, CancellationToken cancellationToken);
    void Add(DroneMaintenanceRecord record);
}
