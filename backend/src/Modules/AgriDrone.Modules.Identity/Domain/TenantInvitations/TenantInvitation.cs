using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.TenantInvitations
{
    public sealed class TenantInvitation : Entity
    {
        public TenantInvitation(Guid id, Guid tenantId, string email, TenantMemberRole role, string tokenHash, TenantInvitationStatus status, Guid invitedByUserId, DateTimeOffset expiredAt, DateTimeOffset createdAt)
        {
            Id = id;
            TenantId = tenantId;
            Email = email;
            Role = role;
            TokenHash = tokenHash;
            Status = status;
            InvitedByUserId = invitedByUserId;
            ExpiresAt = expiredAt;
            CreatedAt = createdAt;
        }
        public Guid TenantId { get; private set; }

        public string Email { get; private set; } = null!;

        public TenantMemberRole Role { get; private set; }

        public string TokenHash { get; private set; } = null!;

        public TenantInvitationStatus Status { get; private set; }

        public Guid InvitedByUserId { get; private set; }

        public Guid? AcceptedByUserId { get; private set; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset? AcceptedAt { get; private set; }

        public static TenantInvitation Create(Guid tenantId, string email, TenantMemberRole role, string tokenHash, TenantInvitationStatus status, Guid invitedByUserId, DateTimeOffset expiredAt, DateTimeOffset createdAt)
        {
            return new TenantInvitation
           (
                Guid.NewGuid(),
                tenantId,
                email,
                role,
                tokenHash,
                status,
                invitedByUserId,
                expiredAt,
                createdAt
           );
        }
    }
}
