using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Infrastructure.Repositories
{
    internal sealed class FarmZoneRepository(
        FarmsDbContext context) : IFarmZoneRepository
    {
        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, Guid farmId, string code, Guid? excludingZoneId = null, CancellationToken cancellationToken = default)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();

            return context.FarmZones
                .AnyAsync(farmZone =>
                farmZone.Farm.TenantId == tenantId &&
                farmZone.FarmId == farmId &&
                farmZone.Code == normalizedCode &&
                farmZone.DeletedAt == null &&
                (!excludingZoneId.HasValue ||
                farmZone.Id != excludingZoneId), cancellationToken);
        }

        public void Add(FarmZone zone)
        {
            ArgumentNullException.ThrowIfNull(zone);
            context.FarmZones.Add(zone);
        }

        public Task<FarmZone?> GetByCodeAsync(Guid tenantId, Guid farmId, string code, CancellationToken cancellationToken = default)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return context.FarmZones
                .AsNoTracking()
                .SingleOrDefaultAsync(farmZone =>
                farmZone.Farm.TenantId == tenantId &&
                farmZone.Code == code &&
                farmZone.DeletedAt == null, cancellationToken);
        }

        public Task<FarmZone?> GetByIdAsync(Guid tenantId, Guid farmId, Guid zoneId, CancellationToken cancellationToken = default)
            => context.FarmZones
            .AsNoTracking()
            .SingleOrDefaultAsync(farmZone =>
            farmZone.Farm.TenantId == tenantId &&
            farmZone.FarmId == farmId &&
            farmZone.Id == zoneId &&
            farmZone.DeletedAt == null, cancellationToken);
    }
}
