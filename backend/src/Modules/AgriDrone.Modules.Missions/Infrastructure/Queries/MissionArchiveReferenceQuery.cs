using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class MissionArchiveReferenceQuery(
    MissionsDbContext dbContext)
    : IMissionArchiveReferenceQuery
{
    public Task<int> CountActiveForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default) =>
        dbContext.DroneMissions
            .AsNoTracking()
            .CountAsync(
                mission =>
                    mission.TenantId == tenantId &&
                    mission.FarmId == farmId &&
                    mission.ZoneId == zoneId &&
                    mission.Status != MissionStatus.Completed &&
                    mission.Status != MissionStatus.Cancelled &&
                    mission.Status != MissionStatus.FlightFailed,
                cancellationToken);

    public Task<int> CountActiveForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.DroneMissions
            .AsNoTracking()
            .CountAsync(
                mission =>
                    mission.TenantId == tenantId &&
                    mission.FarmId == farmId &&
                    mission.Status != MissionStatus.Completed &&
                    mission.Status != MissionStatus.Cancelled &&
                    mission.Status != MissionStatus.FlightFailed,
                cancellationToken);
}
