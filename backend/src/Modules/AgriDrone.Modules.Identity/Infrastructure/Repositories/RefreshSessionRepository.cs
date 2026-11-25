using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class RefreshSessionRepository(IdentityDbContext dbContext) : IRefreshSessionRepository
{
    public async Task<ObservedRefreshTokenLookup?> FindObservedByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await (
            from token in dbContext.RefreshSessionTokens.AsNoTracking()
            join session in dbContext.RefreshSessions.AsNoTracking()
                on token.SessionId equals session.Id
            where token.TokenHash == tokenHash
            select new ObservedRefreshTokenLookup(
                token.Id,
                token.SessionId,
                token.Generation,
                token.UsedAt,
                session.UserId,
                session.TenantId,
                session.TenantMembershipId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<LockedSessionIssueContext?> LockForSessionCreationAsync(
        Guid userId,
        Guid? tenantId,
        Guid? membershipId,
        CancellationToken cancellationToken = default)
    {
        // Consistent lock order: tenant (if any) -> user -> membership / system roles
        Tenant? tenant = null;
        if (tenantId.HasValue)
        {
            tenant = await dbContext.Tenants
                .FromSqlInterpolated($"SELECT * FROM identity.tenants WHERE id = {tenantId.Value} FOR SHARE")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
            if (tenant is null)
            {
                return null;
            }
        }

        // Login may already track the user read before checking the password.
        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM identity.users WHERE id = {userId} FOR SHARE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        TenantMembership? membership = null;
        if (membershipId.HasValue)
        {
            membership = await dbContext.TenantMemberships
                .FromSqlInterpolated($"SELECT *, xmin FROM identity.tenant_memberships WHERE id = {membershipId.Value} FOR SHARE")
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }

        var systemRoles = await ReadLockedSystemRoleCodesAsync(userId, cancellationToken);
        return new LockedSessionIssueContext(user, tenant, membership, systemRoles);
    }

    public async Task<LockedRefreshContext?> LockForRefreshAsync(
        ObservedRefreshTokenLookup observed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observed);

        // Lock order: tenant (if any) -> user -> membership / roles -> session -> token.
        // Every query re-reads committed row data after taking its lock (Read Committed safe).
        Tenant? tenant = null;
        if (observed.TenantId.HasValue)
        {
            tenant = await dbContext.Tenants
                .FromSqlInterpolated($"SELECT * FROM identity.tenants WHERE id = {observed.TenantId.Value} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        var user = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM identity.users WHERE id = {observed.UserId} FOR SHARE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        TenantMembership? membership = null;
        if (observed.TenantMembershipId.HasValue)
        {
            membership = await dbContext.TenantMemberships
                .FromSqlInterpolated($"SELECT *, xmin FROM identity.tenant_memberships WHERE id = {observed.TenantMembershipId.Value} FOR SHARE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        var systemRoles = await ReadLockedSystemRoleCodesAsync(observed.UserId, cancellationToken);

        var session = await dbContext.RefreshSessions
            .FromSqlInterpolated($"SELECT *, xmin FROM identity.refresh_sessions WHERE id = {observed.SessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        var token = await dbContext.RefreshSessionTokens
            .FromSqlInterpolated($"SELECT * FROM identity.refresh_session_tokens WHERE id = {observed.TokenId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (token is null)
        {
            return null;
        }

        return new LockedRefreshContext(session, token, user, tenant, membership, systemRoles);
    }

    public async Task<RefreshSession?> LockSessionForLogoutAsync(
        ObservedRefreshTokenLookup observed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observed);

        if (observed.TenantId.HasValue)
        {
            _ = await dbContext.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM identity.tenants WHERE id = {observed.TenantId.Value} FOR SHARE
                """).FirstOrDefaultAsync(cancellationToken);
        }

        _ = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM identity.users WHERE id = {observed.UserId} FOR SHARE
            """).FirstOrDefaultAsync(cancellationToken);

        if (observed.TenantMembershipId.HasValue)
        {
            _ = await dbContext.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM identity.tenant_memberships WHERE id = {observed.TenantMembershipId.Value} FOR SHARE
                """).FirstOrDefaultAsync(cancellationToken);
        }

        return await dbContext.RefreshSessions
            .FromSqlInterpolated($"SELECT *, xmin FROM identity.refresh_sessions WHERE id = {observed.SessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<User?> LockUserForUpdateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM identity.users WHERE id = {userId} AND deleted_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<User?> LockUserByEmailForUpdateAsync(
        string email,
        CancellationToken cancellationToken = default) =>
        dbContext.Users
            .FromSqlInterpolated($"""
                SELECT * FROM identity.users
                WHERE email = CAST({email} AS citext) AND deleted_at IS NULL
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> RevokeAllActiveByUserIdAsync(
        Guid userId,
        string reason,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var activeSessions = await dbContext.RefreshSessions
            .FromSqlInterpolated($"""
                SELECT *, xmin FROM identity.refresh_sessions
                WHERE user_id = {userId} AND revoked_at IS NULL
                ORDER BY id
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);

        var revokedCount = 0;
        foreach (var session in activeSessions)
        {
            if (session.Revoke(reason, revokedAt))
            {
                revokedCount++;
            }
        }

        return revokedCount;
    }

    public void AddSession(RefreshSession session, RefreshSessionToken initialToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(initialToken);
        dbContext.RefreshSessions.Add(session);
        dbContext.RefreshSessionTokens.Add(initialToken);
    }

    public void AddRotatedToken(RefreshSessionToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        dbContext.RefreshSessionTokens.Add(token);
    }

    private async Task<IReadOnlyCollection<string>> ReadLockedSystemRoleCodesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Database.SqlQuery<string>($"""
            SELECT r.code AS "Value"
            FROM identity.user_roles ur
            JOIN identity.roles r ON r.id = ur.role_id
            WHERE ur.user_id = {userId}
            FOR SHARE OF ur, r
            """).ToArrayAsync(cancellationToken);
    }
}
