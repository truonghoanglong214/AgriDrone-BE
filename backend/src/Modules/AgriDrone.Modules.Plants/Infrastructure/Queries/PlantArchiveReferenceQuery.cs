using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Queries;

internal sealed class PlantArchiveReferenceQuery(
    PlantsDbContext dbContext)
    : IPlantArchiveReferenceQuery
{
    public async Task<ZonePlantReferences> GetZoneReferencesAsync(
        Guid farmId,
        Guid zoneId,
        CancellationToken cancellationToken = default)
    {
        var plantIds = await dbContext.Plants
            .AsNoTracking()
            .Where(
                plant =>
                    plant.FarmId == farmId &&
                    plant.ZoneId == zoneId)
            .Select(plant => plant.Id)
            .ToArrayAsync(cancellationToken);

        if (plantIds.Length == 0)
        {
            return new ZonePlantReferences(
                plantIds,
                Array.Empty<Guid>());
        }

        var scanIds = await dbContext.PlantScans
            .AsNoTracking()
            .Where(
                scan =>
                    scan.FarmId == farmId &&
                    plantIds.Contains(scan.PlantId))
            .Select(scan => scan.Id)
            .ToArrayAsync(cancellationToken);

        return new ZonePlantReferences(plantIds, scanIds);
    }
}
