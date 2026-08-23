using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Invitations.EmailDelivery
{
    internal sealed record TenantInvitationEmailDeliveryResult(
    Guid InvitationId,
    string Email,
    bool Sent,
    string? SkipReason = null)
    {
        public static TenantInvitationEmailDeliveryResult EmailSent(
            Guid invitationId,
            string email) =>
            new(invitationId, email, Sent: true);

        public static TenantInvitationEmailDeliveryResult Skipped(
            Guid invitationId,
            string email,
            string reason) =>
            new(invitationId, email, Sent: false, reason);
    }
}
