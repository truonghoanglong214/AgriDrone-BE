using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories;

internal sealed class PlantRepository(PlantsDbContext context)
    : IPlantRepository
{
    public Task<Plant?> GetByIdAsync(
        Guid farmId,
        Guid zoneId,
        Guid plantId,
        CancellationToken cancellationToken = default)
    {
        return context.Plants.SingleOrDefaultAsync(
            plant =>
                plant.Id == plantId &&
                plant.FarmId == farmId &&
                plant.ZoneId == zoneId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Plant>> GetByIdsAsync(
        Guid farmId,
        Guid zoneId,
        IReadOnlyCollection<Guid> plantIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plantIds);

        if (plantIds.Count == 0)
        {
            return [];
        }

        var distinctIds = plantIds.Distinct().ToArray();

        return await context.Plants
            .Where(plant =>
                plant.FarmId == farmId &&
                plant.ZoneId == zoneId &&
                distinctIds.Contains(plant.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        Guid farmId,
        string plantCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plantCode);
        var normalizedCode = plantCode.Trim().ToUpperInvariant();

        return context.Plants
            .AsNoTracking()
            .AnyAsync(
                plant =>
                    plant.FarmId == farmId &&
                    plant.PlantCode == normalizedCode,
                cancellationToken);
    }

    public Task<bool> GridPositionExistsAsync(
        Guid zoneId,
        int rowIndex,
        int columnIndex,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rowIndex, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columnIndex, 1);

        return context.Plants
            .AsNoTracking()
            .AnyAsync(
                plant =>
                    plant.ZoneId == zoneId &&
                    plant.RowIndex == rowIndex &&
                    plant.ColumnIndex == columnIndex &&
                    (plant.LifecycleStatus == PlantLifecycleStatus.Active ||
                     plant.LifecycleStatus == PlantLifecycleStatus.Missing),
                cancellationToken);
    }

    public void Add(Plant plant)
    {
        ArgumentNullException.ThrowIfNull(plant);
        context.Plants.Add(plant);
    }

    public void AddChangeEvent(PlantChangeEvent changeEvent)
    {
        ArgumentNullException.ThrowIfNull(changeEvent);
        context.PlantChangeEvents.Add(changeEvent);
    }
}
