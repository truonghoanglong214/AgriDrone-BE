using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.RefreshSessions;

public sealed class RefreshSession : Entity
{
    public const string RevokeReasonLogout = "LOGOUT";
    public const string RevokeReasonTokenReuse = "TOKEN_REUSE";
    public const string RevokeReasonPasswordChanged = "PASSWORD_CHANGED";
    public const string RevokeReasonPasswordReset = "PASSWORD_RESET";
    public const string RevokeReasonUserDeactivated = "USER_DEACTIVATED";
    public const string RevokeReasonAuthorizationInvalid = "AUTHORIZATION_INVALID";

    private RefreshSession()
    {
    }

    public Guid UserId { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? TenantMembershipId { get; private set; }

    public int AuthenticationVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset AbsoluteExpiresAt { get; private set; }

    public DateTimeOffset? LastRotatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevocationReason { get; private set; }

    public uint Version { get; private set; }

    public User User { get; private set; } = null!;

    public TenantMembership? TenantMembership { get; private set; }

    public ICollection<RefreshSessionToken> Tokens { get; private set; } = [];

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsExpired(DateTimeOffset now) => now >= AbsoluteExpiresAt;

    public static RefreshSession Create(
        Guid id,
        Guid userId,
        Guid? tenantId,
        Guid? tenantMembershipId,
        int authenticationVersion,
        DateTimeOffset createdAt,
        DateTimeOffset absoluteExpiresAt)
    {
        DomainGuard.NotEmpty(id);
        DomainGuard.NotEmpty(userId);
        DomainGuard.Utc(createdAt);
        DomainGuard.Utc(absoluteExpiresAt);

        if (tenantId.HasValue != tenantMembershipId.HasValue)
        {
            throw new ArgumentException("TenantId and TenantMembershipId must both be supplied or both be null.");
        }

        if (tenantId.HasValue)
        {
            DomainGuard.NotEmpty(tenantId.Value, nameof(tenantId));
            DomainGuard.NotEmpty(tenantMembershipId!.Value, nameof(tenantMembershipId));
        }

        if (authenticationVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(authenticationVersion), "AuthenticationVersion must be >= 1.");
        }

        if (absoluteExpiresAt <= createdAt)
        {
            throw new ArgumentException("AbsoluteExpiresAt must be greater than CreatedAt.", nameof(absoluteExpiresAt));
        }

        return new RefreshSession
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            TenantMembershipId = tenantMembershipId,
            AuthenticationVersion = authenticationVersion,
            CreatedAt = createdAt,
            AbsoluteExpiresAt = absoluteExpiresAt
        };
    }

    public void RecordRotation(DateTimeOffset rotatedAt)
    {
        DomainGuard.Utc(rotatedAt);
        LastRotatedAt = rotatedAt;
    }

    public bool Revoke(string reason, DateTimeOffset revokedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        DomainGuard.Utc(revokedAt);

        if (IsRevoked)
        {
            return false;
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 100)
        {
            normalizedReason = normalizedReason[..100];
        }

        RevokedAt = revokedAt;
        RevocationReason = normalizedReason;
        return true;
    }
}
