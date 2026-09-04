using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Repositories;

internal sealed class DroneMissionRepository(
    MissionsDbContext dbContext)
    : IDroneMissionRepository
{
    public Task<DroneMission?> GetByIdAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.DroneMissions
            .SingleOrDefaultAsync(
                mission =>
                    mission.Id == missionId &&
                    mission.TenantId == tenantId &&
                    mission.FarmId == farmId,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        Guid farmId,
        string missionCode,
        CancellationToken cancellationToken = default)
    {
        return dbContext.DroneMissions.AnyAsync(
            mission =>
                mission.FarmId == farmId &&
                mission.MissionCode == missionCode,
            cancellationToken);
    }

    public void Add(DroneMission mission)
    {
        dbContext.DroneMissions.Add(mission);
    }
}
