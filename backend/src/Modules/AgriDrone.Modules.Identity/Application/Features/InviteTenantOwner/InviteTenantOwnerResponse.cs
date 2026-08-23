using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.InviteTenantOwner
{
    public sealed record InviteTenantOwnerResponse(
         Guid InvitationId,
        string Email,
        DateTimeOffset ExpiresAt);
}
