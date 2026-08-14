using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.Tenants
{
    public interface ITenantMembershipRepository
    {
        void Add(TenantMembership tenantMembership);
    }
}
