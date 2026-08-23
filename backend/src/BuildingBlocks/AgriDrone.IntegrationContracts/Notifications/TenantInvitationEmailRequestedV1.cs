using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.IntegrationContracts.Notifications
{
    public sealed record TenantInvitationEmailRequestedV1(
    Guid InvitationId,
    string PlainTextToken);
}
