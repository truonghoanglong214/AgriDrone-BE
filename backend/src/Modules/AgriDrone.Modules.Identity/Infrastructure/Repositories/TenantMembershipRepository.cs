using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories
{
    internal sealed class TenantMembershipRepository(IdentityDbContext context) : ITenantMembershipRepository
    {  
        public void Add(TenantMembership tenantMembership) =>context.TenantMemberships.Add(tenantMembership);      
    }
}
