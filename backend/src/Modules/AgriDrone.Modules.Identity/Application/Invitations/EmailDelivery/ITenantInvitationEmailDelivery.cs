using AgriDrone.SharedKernel.Application;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Invitations.EmailDelivery
{
    internal interface ITenantInvitationEmailDelivery
    {
        Task<Result<TenantInvitationEmailDeliveryResult>> DeliverAsync(
            Guid messageTenantId,
            Guid invitationId,
            string plainTextToken,
            CancellationToken cancellationToken);
    }
}
