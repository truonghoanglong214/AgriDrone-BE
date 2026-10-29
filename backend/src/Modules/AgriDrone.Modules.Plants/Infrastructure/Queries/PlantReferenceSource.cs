using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Caching;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class PlantReferenceSource(
    PlantsDbContext dbContext,
    IMissionPlanningReferenceQuery farmReferenceQuery)
    : IPlantReferenceSource
{
    public async Task<IReadOnlyList<PlantReferenceV1>> LoadActiveByZoneAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId,
        CancellationToken cancellationToken = default)
    {
        ValidateScope(tenantId, farmId, zoneId, mapVersionId);

        var mapExists =
            await farmReferenceQuery.IsPublishedFarmBaseMapVersionAsync(
                tenantId,
                farmId,
                mapVersionId,
                cancellationToken);
        if (!mapExists)
        {
            return [];
        }

        var scopedPlantIds = await dbContext.Database.SqlQueryRaw<Guid>(
                "SELECT membership.plant_id AS \"Value\" " +
                "FROM farm.farm_zone_plant_memberships AS membership " +
                "WHERE membership.farm_id = {0} AND membership.zone_id = {1} " +
                "AND membership.effective_to IS NULL AND membership.is_assigned = TRUE",
                farmId,
                zoneId)
            .ToArrayAsync(cancellationToken);

        var plants = await dbContext.Plants
            .AsNoTracking()
            .Where(plant =>
                plant.FarmId == farmId &&
                scopedPlantIds.Contains(plant.Id) &&
                plant.FarmBaseMapVersionId == mapVersionId &&
                (plant.LifecycleStatus == PlantLifecycleStatus.Active ||
                 plant.LifecycleStatus == PlantLifecycleStatus.Missing))
            .OrderBy(plant => plant.RowIndex)
            .ThenBy(plant => plant.ColumnIndex)
            .ThenBy(plant => plant.Id)
            .Select(plant => new PlantReferenceProjection(
                plant.Id,
                plant.FarmId,
                zoneId,
                plant.LifecycleStatus,
                plant.FarmBaseMapVersionId,
                plant.Location == null ? null : plant.Location.Y,
                plant.Location == null ? null : plant.Location.X,
                plant.RowIndex,
                plant.ColumnIndex,
                plant.LocationAccuracyM))
            .ToArrayAsync(cancellationToken);

        return plants
            .Select(plant => new PlantReferenceV1(
                plant.PlantId,
                plant.FarmId,
                plant.ZoneId,
                ToContractStatus(plant.LifecycleStatus),
                plant.MapVersionId,
                plant.Latitude,
                plant.Longitude,
                plant.RowIndex,
                plant.ColumnIndex,
                plant.LocationAccuracyM.HasValue
                    ? (double?)plant.LocationAccuracyM.Value
                    : null))
            .ToArray();
    }

    private static string ToContractStatus(
        PlantLifecycleStatus lifecycleStatus) =>
        lifecycleStatus switch
        {
            PlantLifecycleStatus.Active => "ACTIVE",
            PlantLifecycleStatus.Missing => "MISSING",
            _ => throw new InvalidOperationException(
                $"Lifecycle '{lifecycleStatus}' is not eligible for a Plant reference.")
        };

    private static void ValidateScope(
        Guid tenantId,
        Guid farmId,
        Guid zoneId,
        Guid mapVersionId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(farmId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(zoneId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(mapVersionId, Guid.Empty);
    }

    private sealed record PlantReferenceProjection(
        Guid PlantId,
        Guid FarmId,
        Guid ZoneId,
        PlantLifecycleStatus LifecycleStatus,
        Guid? MapVersionId,
        double? Latitude,
        double? Longitude,
        int? RowIndex,
        int? ColumnIndex,
        decimal? LocationAccuracyM);
}
