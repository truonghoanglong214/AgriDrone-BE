using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.Tenants
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<Tenant?> GetByCodeAsync(string tenantCode, CancellationToken cancellationToken);
        void Add(Tenant tenant);
    }
}
