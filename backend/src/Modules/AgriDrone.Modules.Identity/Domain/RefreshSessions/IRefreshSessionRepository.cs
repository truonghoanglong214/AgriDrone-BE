using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;

namespace AgriDrone.Modules.Identity.Domain.RefreshSessions;

public sealed record ObservedRefreshTokenLookup(
    Guid TokenId,
    Guid SessionId,
    int Generation,
    DateTimeOffset? ObservedUsedAt,
    Guid UserId,
    Guid? TenantId,
    Guid? TenantMembershipId);

public sealed record LockedRefreshContext(
    RefreshSession Session,
    RefreshSessionToken Token,
    User User,
    Tenant? Tenant,
    TenantMembership? Membership,
    IReadOnlyCollection<string> SystemRoles);

public sealed record LockedSessionIssueContext(
    User User,
    Tenant? Tenant,
    TenantMembership? Membership,
    IReadOnlyCollection<string> SystemRoles);

public interface IRefreshSessionRepository
{
    Task<ObservedRefreshTokenLookup?> FindObservedByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<LockedSessionIssueContext?> LockForSessionCreationAsync(
        Guid userId,
        Guid? tenantId,
        Guid? membershipId,
        CancellationToken cancellationToken = default);

    Task<LockedRefreshContext?> LockForRefreshAsync(
        ObservedRefreshTokenLookup observed,
        CancellationToken cancellationToken = default);

    Task<RefreshSession?> LockSessionForLogoutAsync(
        ObservedRefreshTokenLookup observed,
        CancellationToken cancellationToken = default);

    Task<User?> LockUserForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<User?> LockUserByEmailForUpdateAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<int> RevokeAllActiveByUserIdAsync(
        Guid userId,
        string reason,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default);

    void AddSession(RefreshSession session, RefreshSessionToken initialToken);

    void AddRotatedToken(RefreshSessionToken token);
}
