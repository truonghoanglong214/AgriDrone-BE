using AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Queries;

internal interface IFarmZoneQueries
{
    Task<IReadOnlyList<ZoneListItemResponse>> GetByFarmAsync(
        Guid tenantId,
        Guid farmId,
        CancellationToken cancellationToken);
}
