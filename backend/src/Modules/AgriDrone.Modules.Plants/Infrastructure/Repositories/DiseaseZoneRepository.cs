using AgriDrone.Modules.Plants.Domain.DiseaseZones;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Infrastructure.Repositories;

internal sealed class DiseaseZoneRepository(PlantsDbContext context)
    : IDiseaseZoneRepository
{
    public Task<DiseaseZone?> GetByIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid diseaseZoneId,
        CancellationToken cancellationToken = default)
        => context.DiseaseZones
            .Include(zone => zone.Memberships)
            .SingleOrDefaultAsync(
                zone => zone.TenantId == tenantId &&
                        zone.FarmId == farmId &&
                        zone.Id == diseaseZoneId,
                cancellationToken);

    public Task<DiseaseZone?> GetCurrentPublishedAsync(
        Guid tenantId,
        Guid farmId,
        Guid zoneKey,
        CancellationToken cancellationToken = default)
        => context.DiseaseZones
            .Include(zone => zone.Memberships)
            .SingleOrDefaultAsync(
                zone => zone.TenantId == tenantId &&
                        zone.FarmId == farmId &&
                        zone.ZoneKey == zoneKey &&
                        zone.Status == DiseaseZoneStatus.Published,
                cancellationToken);

    public void Add(DiseaseZone diseaseZone)
    {
        ArgumentNullException.ThrowIfNull(diseaseZone);
        context.DiseaseZones.Add(diseaseZone);
    }

    public void Update(DiseaseZone diseaseZone)
    {
        ArgumentNullException.ThrowIfNull(diseaseZone);
        context.DiseaseZones.Update(diseaseZone);
    }
}
