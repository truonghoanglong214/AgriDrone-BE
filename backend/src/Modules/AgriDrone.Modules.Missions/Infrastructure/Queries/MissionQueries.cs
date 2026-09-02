using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application
    .Features.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class MissionQueries(
    MissionsDbContext dbContext)
    : IMissionQueries
{
    public async Task<MissionResponse?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        var mission = await dbContext.DroneMissions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == missionId &&
                    item.TenantId == tenantId &&
                    item.FarmId == farmId,
                cancellationToken);

        return mission is null
            ? null
            : MissionResponseMapper.Map(mission);
    }
}
