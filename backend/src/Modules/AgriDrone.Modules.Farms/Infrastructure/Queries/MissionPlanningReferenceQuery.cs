using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class MissionPlanningReferenceQuery(
    FarmsDbContext dbContext)
    : IMissionPlanningReferenceQuery
{
    public Task<bool> IsActiveZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FarmZones
            .AsNoTracking()
            .AnyAsync(
                zone =>
                    zone.Id == zoneId &&
                    zone.FarmId == farmId &&
                    zone.Status == GeneralStatus.Active &&
                    zone.DeletedAt == null &&
                    zone.Farm.TenantId == tenantId &&
                    zone.Farm.Status == GeneralStatus.Active &&
                    zone.Farm.DeletedAt == null,
                cancellationToken);
    }

    public Task<bool> IsConfirmedMapVersionAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.ZoneMapVersions
            .AsNoTracking()
            .AnyAsync(
                mapVersion =>
                    mapVersion.Id == mapVersionId &&
                    mapVersion.FarmId == farmId &&
                    mapVersion.ZoneId == zoneId &&
                    mapVersion.Status == MapVersionStatus.Confirmed &&
                    mapVersion.Zone.Status == GeneralStatus.Active &&
                    mapVersion.Zone.DeletedAt == null &&
                    mapVersion.Zone.Farm.TenantId == tenantId &&
                    mapVersion.Zone.Farm.Status == GeneralStatus.Active &&
                    mapVersion.Zone.Farm.DeletedAt == null,
                cancellationToken);
    }
}
