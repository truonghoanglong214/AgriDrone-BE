using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class FarmAssignmentReferenceQuery(
    FarmsDbContext dbContext) : IFarmAssignmentReferenceQuery
{
    public Task<SystemManagerFarmReference?> GetActiveFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.Farms
            .AsNoTracking()
            .Where(farm =>
                farm.Id == farmId &&
                farm.Status == GeneralStatus.Active &&
                farm.DeletedAt == null)
            .Select(farm => new SystemManagerFarmReference(
                farm.TenantId,
                farm.Id,
                farm.Code,
                farm.Name,
                farm.Address,
                farm.AreaHectares))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SystemManagerFarmReference>>
        GetActiveFarmsAsync(
        IReadOnlyCollection<Guid> farmIds,
        CancellationToken cancellationToken = default)
    {
        if (farmIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Farms
            .AsNoTracking()
            .Where(farm =>
                farmIds.Contains(farm.Id) &&
                farm.Status == GeneralStatus.Active &&
                farm.DeletedAt == null)
            .OrderBy(farm => farm.Code)
            .ThenBy(farm => farm.Id)
            .Select(farm => new SystemManagerFarmReference(
                farm.TenantId,
                farm.Id,
                farm.Code,
                farm.Name,
                farm.Address,
                farm.AreaHectares))
            .ToArrayAsync(cancellationToken);
    }

    public Task<bool> IsActiveFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken = default) =>
        dbContext.Farms
            .AsNoTracking()
            .AnyAsync(
                farm =>
                    farm.Id == farmId &&
                    farm.TenantId == tenantId &&
                    farm.Status == GeneralStatus.Active &&
                    farm.DeletedAt == null,
                cancellationToken);

    public async Task<IReadOnlyCollection<FarmAssignmentReference>>
        GetActiveFarmsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Farms
            .AsNoTracking()
            .Where(farm =>
                farm.TenantId == tenantId &&
                farm.Status == GeneralStatus.Active &&
                farm.DeletedAt == null)
            .OrderBy(farm => farm.Code)
            .ThenBy(farm => farm.Id)
            .Select(farm => new FarmAssignmentReference(
                farm.Id,
                farm.Code,
                farm.Name,
                farm.Address,
                farm.AreaHectares))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<FarmAssignmentZoneReference>>
        GetActiveZonesAsync(
            Guid tenantId,
            IReadOnlyCollection<Guid> farmIds,
            CancellationToken cancellationToken = default)
    {
        if (farmIds.Count == 0)
        {
            return [];
        }

        return await dbContext.FarmZones
            .AsNoTracking()
            .Where(zone =>
                farmIds.Contains(zone.FarmId) &&
                zone.Farm.TenantId == tenantId &&
                zone.Farm.Status == GeneralStatus.Active &&
                zone.Farm.DeletedAt == null &&
                zone.Status == GeneralStatus.Active &&
                zone.DeletedAt == null)
            .OrderBy(zone => zone.FarmId)
            .ThenBy(zone => zone.Code)
            .ThenBy(zone => zone.Id)
            .Select(zone => new FarmAssignmentZoneReference(
                zone.FarmId,
                zone.Id,
                zone.Code,
                zone.Name,
                zone.AreaHectares))
            .ToArrayAsync(cancellationToken);
    }
}
