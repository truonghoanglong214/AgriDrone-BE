using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Domain.SystemManagerInvitations
{
    public sealed class SystemManagerInvitation : Entity
    {
        private SystemManagerInvitation()
        {
        }

        private SystemManagerInvitation(
            string email,
            string tokenHash,
            Guid invitedByUserId,
            DateTimeOffset expiresAt,
            DateTimeOffset createdAt)
        {
            Id = Guid.NewGuid();
            Email = email;
            TokenHash = tokenHash;
            InvitedByUserId = invitedByUserId;
            ExpiresAt = expiresAt;
            CreatedAt = createdAt;
            Status = SystemManagerInvitationStatus.Pending;
        }

        public string Email { get; private set; } = null!;

        public string TokenHash { get; private set; } = null!;

        public SystemManagerInvitationStatus Status { get; private set; }

        public Guid InvitedByUserId { get; private set; }

        public Guid? AcceptedByUserId { get; private set; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset? AcceptedAt { get; private set; }

        public User InvitedByUser { get; private set; } = null!;

        public User? AcceptedByUser { get; private set; }

        public static SystemManagerInvitation Create(
            string email,
            string tokenHash,
            Guid invitedByUserId,
            DateTimeOffset expiresAt,
            DateTimeOffset createdAt)
        {
            DomainGuard.NotEmpty(invitedByUserId);
            DomainGuard.Utc(expiresAt);
            DomainGuard.Utc(createdAt);

            if (expiresAt <= createdAt)
            {
                throw new ArgumentOutOfRangeException(nameof(expiresAt), 
                    expiresAt,
                "Invitation expiration time must be later than its creation time.");
            }

            return new SystemManagerInvitation(
                email.Trim().ToLowerInvariant(),
                tokenHash,
                invitedByUserId,
                expiresAt,
                createdAt);
        }

        public bool CanBeAccepted(DateTimeOffset now)
        {
            DomainGuard.Utc(now);

            return Status == SystemManagerInvitationStatus.Pending &&
                   now < ExpiresAt;
        }

        public void Accept(Guid userId, DateTimeOffset acceptedAt)
        {
            DomainGuard.NotEmpty(userId);
            DomainGuard.Utc(acceptedAt);

            if (!CanBeAccepted(acceptedAt))
            {
                throw new InvalidOperationException(
                    "Only a pending, unexpired invitation can be accepted.");
            }

            Status = SystemManagerInvitationStatus.Accepted;
            AcceptedByUserId = userId;
            AcceptedAt = acceptedAt;
        }

        public void MarkExpired(DateTimeOffset now)
        {
            DomainGuard.Utc(now);

            if (Status != SystemManagerInvitationStatus.Pending ||
                now < ExpiresAt)
            {
                throw new InvalidOperationException(
                    "Only an expired pending invitation can be marked expired.");
            }

            Status = SystemManagerInvitationStatus.Expired;
        }

        public void Revoke(DateTimeOffset now)
        {
            DomainGuard.Utc(now);

            if (Status != SystemManagerInvitationStatus.Pending)
            {
                throw new InvalidOperationException(
                    "Only a pending invitation can be revoked.");
            }

            Status = SystemManagerInvitationStatus.Revoked;
        }
    }
}
