using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.Tenants
{
    public interface ITenantRepository
    {
        Task<Tenant?> GetByCodeAsync(string tenantCode);
        void Add(Tenant tenant);
    }
}
