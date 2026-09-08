using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class FarmArchiveDependencyQuery(
    FarmsDbContext dbContext,
    IMissionArchiveReferenceQuery missionReferenceQuery,
    IFieldTaskArchiveReferenceQuery fieldTaskReferenceQuery,
    IPlantArchiveReferenceQuery plantReferenceQuery)
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

        var plantReferences =
            await plantReferenceQuery.GetZoneReferencesAsync(
                farmId,
                zoneId,
                cancellationToken);

        var openFieldTaskCount =
            await fieldTaskReferenceQuery.CountOpenForZoneReferencesAsync(
                farmId,
                plantReferences.PlantIds,
                plantReferences.ScanIds,
                cancellationToken);

        return new ArchiveDependencySummary(
            ActiveZoneCount: 0,
            activeMissionCount,
            openFieldTaskCount);
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

        var openFieldTaskCount =
            await fieldTaskReferenceQuery.CountOpenForFarmAsync(
                farmId,
                cancellationToken);

        return new ArchiveDependencySummary(
            activeZoneCount,
            activeMissionCount,
            openFieldTaskCount);
    }
}
