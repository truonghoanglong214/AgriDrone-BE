using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.TenantInvitations
{
    public interface ITenantInvitationRepository
    {

        bool CanBeAccepted(DateTimeOffset now);

        void Accept(Guid userId, DateTimeOffset now);

        void Revoke(DateTimeOffset now);
    }
}
