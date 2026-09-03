using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class DroneStatusChangeRepository(
    MissionsDbContext dbContext)
    : IDroneStatusChangeRepository
{
    public void Add(DroneStatusChange statusChange)
    {
        dbContext.DroneStatusChanges.Add(statusChange);
    }
}