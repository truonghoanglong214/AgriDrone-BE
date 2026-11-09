using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Infrastructure.Repositories
{
    internal sealed class FarmRepository(
        FarmsDbContext context) : IFarmRepository
    {
        public Task<bool> ActiveCodeExistsAsync(Guid tenantId, string code, Guid? excludingFarmId = null, CancellationToken cancellationToken = default)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return context.Farms
                .AnyAsync(farm => farm.TenantId == tenantId && farm.Code == normalizedCode && farm.DeletedAt == null && (!excludingFarmId.HasValue || farm.Id != excludingFarmId.Value), cancellationToken);
        }

        public void Add(Farm farm)
        {
            ArgumentNullException.ThrowIfNull(farm);
            context.Farms.Add(farm);
        }

        public Task<Farm?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            return context.Farms
            .AsNoTracking()
            .SingleOrDefaultAsync(farm => farm.TenantId == tenantId && farm.Code == normalizedCode && farm.DeletedAt == null, cancellationToken);
        }

        public Task<Farm?> GetByIdAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken)
            // Tracked on purpose: callers mutate the returned Farm (Version++ in domain methods) and
            // later call Update(farm). With AsNoTracking, EF has no original "version" snapshot from the
            // DB, so the concurrency-token WHERE clause ends up comparing against the already-incremented
            // value and never matches — every write fails with a false "concurrent modification" error.
            => context.Farms
            .SingleOrDefaultAsync(farm => farm.TenantId == tenantId && farm.Id == farmId && farm.DeletedAt == null, cancellationToken);

        public Task<Farm?> GetByIdIncludingArchivedAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
            // Tracked for the same reason as GetByIdAsync above (used by RestoreFarmHandler, which mutates then Updates).
            => context.Farms
            .SingleOrDefaultAsync(farm => farm.TenantId == tenantId && farm.Id == farmId && farm.IsArchived, cancellationToken);

        public void Update(Farm farm)
            => context.Farms.Update(farm);
    }
}
