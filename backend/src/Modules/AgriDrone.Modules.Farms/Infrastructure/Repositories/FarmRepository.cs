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
                .AnyAsync(farm => farm.TenantId == tenantId && farm.Code == normalizedCode && farm.DeletedAt != null && (!excludingFarmId.HasValue || farm.Id != excludingFarmId.Value), cancellationToken);
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
            => context.Farms
            .AsNoTracking()
            .SingleOrDefaultAsync(farm => farm.TenantId == tenantId && farm.Id == farmId && farm.DeletedAt == null, cancellationToken);

        public void Update(Farm farm)
            => context.Farms.Update(farm);
    }
}
