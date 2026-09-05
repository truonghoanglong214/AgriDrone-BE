using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries;

internal sealed class FarmZoneQueries(FarmsDbContext context)
    : IFarmZoneQueries
{
    public async Task<IReadOnlyList<ZoneListItemResponse>> GetByFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken)
    {
        return await context.FarmZones
            .AsNoTracking()
            .Where(zone =>
                zone.Farm.TenantId == tenantId &&
                zone.Farm.DeletedAt == null &&
                zone.FarmId == farmId &&
                zone.DeletedAt == null)
            .OrderBy(zone => zone.Code)
            .ThenBy(zone => zone.Id)
            .Select(zone => new ZoneListItemResponse(
                zone.Id,
                zone.FarmId,
                zone.Code,
                zone.Name,
                zone.Boundary,
                zone.AreaHectares,
                zone.Status,
                zone.Version,
                zone.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
