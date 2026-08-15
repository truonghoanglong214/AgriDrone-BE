using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories
{
    internal sealed class TenantRepository(IdentityDbContext context) : ITenantRepository
    {
        public void Add(Tenant tenant) => context.Tenants.Add(tenant);

        public Task<Tenant?> GetByCodeAsync(string tenantCode, CancellationToken cancellationToken) => context.Tenants.SingleOrDefaultAsync(t => t.Code == tenantCode && t.Status == GeneralStatus.Active, cancellationToken);

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => context.Tenants.SingleOrDefaultAsync(t => t.Id == id && t.Status == GeneralStatus.Active, cancellationToken);
    }
}
