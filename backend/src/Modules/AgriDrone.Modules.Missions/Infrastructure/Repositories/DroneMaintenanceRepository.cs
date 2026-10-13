using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class DroneMaintenanceRepository(MissionsDbContext db) : IDroneMaintenanceRepository
{
    public Task<DroneMaintenanceRecord?> GetOpenAsync(Guid droneId,
        CancellationToken cancellationToken) =>
        db.DroneMaintenanceRecords.SingleOrDefaultAsync(
            record => record.DroneId == droneId && record.ClosedAt == null,
            cancellationToken);

    public async Task<IReadOnlyList<DroneMaintenanceRecord>> ListAsync(Guid droneId,
        CancellationToken cancellationToken) =>
        await db.DroneMaintenanceRecords.AsNoTracking()
            .Where(record => record.DroneId == droneId)
            .OrderByDescending(record => record.StartedAt)
            .ToListAsync(cancellationToken);

    public void Add(DroneMaintenanceRecord record) => db.DroneMaintenanceRecords.Add(record);
}
