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
            await farmReferenceQuery.IsConfirmedMapVersionAsync(
                tenantId,
                farmId,
                zoneId,
                mapVersionId,
                cancellationToken);
        if (!mapExists)
        {
            return [];
        }

        var plants = await dbContext.Plants
            .AsNoTracking()
            .Where(plant =>
                plant.FarmId == farmId &&
                plant.ZoneId == zoneId &&
                plant.CurrentMapVersionId == mapVersionId &&
                (plant.LifecycleStatus == PlantLifecycleStatus.Active ||
                 plant.LifecycleStatus == PlantLifecycleStatus.Missing))
            .OrderBy(plant => plant.RowIndex)
            .ThenBy(plant => plant.ColumnIndex)
            .ThenBy(plant => plant.Id)
            .Select(plant => new PlantReferenceProjection(
                plant.Id,
                plant.FarmId,
                plant.ZoneId!.Value,
                plant.LifecycleStatus,
                plant.CurrentMapVersionId,
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
