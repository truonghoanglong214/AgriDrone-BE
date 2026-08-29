using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.TenantInvitations;

public sealed class TenantInvitation : Entity
{
    private TenantInvitation()
    {
    }

    private TenantInvitation(
        Guid id,
        Guid tenantId,
        string email,
        TenantMemberRole role,
        TenantInvitationPurpose purpose,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        Role = role;
        Purpose = purpose;
        TokenHash = tokenHash;
        Status = TenantInvitationStatus.Pending;
        InvitedByUserId = invitedByUserId;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; } = null!;

    public TenantMemberRole Role { get; private set; }

    public TenantInvitationPurpose Purpose { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public TenantInvitationStatus Status { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    public User InvitedByUser { get; private set; } = null!;

    public User? AcceptedByUser { get; private set; }

    public static TenantInvitation Create(
        Guid tenantId,
        string email,
        TenantMemberRole role,
        TenantInvitationPurpose purpose,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(invitedByUserId);
        DomainGuard.Utc(expiresAt);
        DomainGuard.Utc(createdAt);

        if (!HasCompatiblePurpose(role, purpose))
        {
            throw new ArgumentException(
                "The invitation purpose is incompatible with the tenant role.",
                nameof(purpose));
        }

        return new TenantInvitation(
            Guid.NewGuid(),
            tenantId,
            email.Trim(),
            role,
            purpose,
            tokenHash,
            invitedByUserId,
            expiresAt,
            createdAt);
    }

    private static bool HasCompatiblePurpose(
        TenantMemberRole role,
        TenantInvitationPurpose purpose) =>
        (role, purpose) switch
        {
            (TenantMemberRole.Owner, TenantInvitationPurpose.OwnerProvisioning) => true,
            (not TenantMemberRole.Owner, TenantInvitationPurpose.Membership) => true,
            _ => false
        };

    public bool CanBeAccepted(DateTimeOffset now)
    {
        DomainGuard.Utc(now);

        return Status == TenantInvitationStatus.Pending && now < ExpiresAt;
    }

    public void Accept(Guid userId, DateTimeOffset now)
    {
        DomainGuard.NotEmpty(userId);
        DomainGuard.Utc(now);

        if (!CanBeAccepted(now))
        {
            throw new InvalidOperationException(
                "Only a pending, unexpired invitation can be accepted.");
        }

        Status = TenantInvitationStatus.Accepted;
        AcceptedByUserId = userId;
        AcceptedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        DomainGuard.Utc(now);

        if (Status != TenantInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only a pending invitation can be revoked.");
        }

        if (now < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(now),
                now,
                "Revocation time cannot be earlier than creation time.");
        }

        Status = TenantInvitationStatus.Revoked;
    }

    public void MarkExpired(DateTimeOffset now)
    {
        DomainGuard.Utc(now);

        if (Status != TenantInvitationStatus.Pending || now < ExpiresAt)
        {
            throw new InvalidOperationException(
                "Only a pending invitation past its expiration time can be marked as expired.");
        }

        Status = TenantInvitationStatus.Expired;
    }
}
