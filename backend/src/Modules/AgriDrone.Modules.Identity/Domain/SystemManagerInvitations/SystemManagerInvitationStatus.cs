using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.SystemManagerInvitations
{

    public enum SystemManagerInvitationStatus
    {
        Pending,
        Accepted,
        Expired,
        Revoked
    }
}
