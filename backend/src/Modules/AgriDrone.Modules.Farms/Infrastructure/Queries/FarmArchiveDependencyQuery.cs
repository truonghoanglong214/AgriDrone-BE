using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

/// <summary>
/// Read-only dependency adapter for archive operations. SurveyOrder can be
/// added here once its schema and internal query port exist.
/// </summary>
internal sealed class LegacyReadOnlyFarmArchiveDependencyQuery(
    FarmsDbContext dbContext,
    IMissionArchiveReferenceQuery missionReferenceQuery)
    : IFarmArchiveDependencyQuery
{
    public async Task<ArchiveDependencySummary> GetForZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        var activeMissionCount =
            await missionReferenceQuery.CountActiveForZoneAsync(
                tenantId,
                farmId,
                zoneId,
                cancellationToken);

        return new ArchiveDependencySummary(
            ActiveZoneCount: 0,
            activeMissionCount);
    }

    public async Task<ArchiveDependencySummary> GetForFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default)
    {
        var activeZoneCount = await dbContext.FarmZones
            .AsNoTracking()
            .CountAsync(
                zone =>
                    zone.FarmId == farmId &&
                    zone.Farm.TenantId == tenantId &&
                    zone.DeletedAt == null,
                cancellationToken);

        var activeMissionCount =
            await missionReferenceQuery.CountActiveForFarmAsync(
                tenantId,
                farmId,
                cancellationToken);

        return new ArchiveDependencySummary(
            activeZoneCount,
            activeMissionCount);
    }
}
