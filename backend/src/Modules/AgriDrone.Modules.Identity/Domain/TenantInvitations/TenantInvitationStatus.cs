using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.TenantInvitations
{
    public enum TenantInvitationStatus
    {
        Pending,
        Accepted,
        Revoked,
        Expired
    }
}
