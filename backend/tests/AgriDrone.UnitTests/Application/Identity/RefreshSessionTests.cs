using AgriDrone.Modules.Identity.Application.Abstractions.Persistence;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.Modules.Identity.Application.Features.RefreshSession;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Configuration;
using AgriDrone.Modules.Identity.Infrastructure.Security;
using AgriDrone.SharedKernel.Domain;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriDrone.UnitTests.Application.Identity;

public sealed class RefreshSessionTests
{
    [Fact]
    public void RefreshTokenServiceGenerates32ByteBase64UrlAndRejectsInvalidTokens()
    {
        var service = new RefreshTokenService();

        var generated = service.Generate();

        Assert.True(service.TryHash(generated.RawToken, out var hash));
        Assert.Equal(generated.TokenHash, hash);
        Assert.Equal(64, hash.Length);

        Assert.False(service.TryHash(null, out _));
        Assert.False(service.TryHash("", out _));
        Assert.False(service.TryHash("not-a-valid-token!", out _));
        Assert.False(service.TryHash(generated.RawToken + "AA", out _));
    }

    [Fact]
    public void RefreshSessionOptionsValidatorEnforcesPositiveOrderedLifetimes()
    {
        var validator = new RefreshSessionOptionsValidator();

        Assert.True(validator.Validate(null, new RefreshSessionOptions
        {
            CookieName = "agridrone_refresh",
            IdleLifetimeDays = 7,
            AbsoluteLifetimeDays = 30,
            AllowedOrigins = ["https://app.agridrone.vn"]
        }).Succeeded);

        Assert.True(validator.Validate(null, new RefreshSessionOptions
        {
            CookieName = "agridrone_refresh",
            IdleLifetimeDays = 14,
            AbsoluteLifetimeDays = 7
        }).Failed);

        Assert.True(validator.Validate(null, new RefreshSessionOptions
        {
            CookieName = "agridrone_refresh",
            IdleLifetimeDays = 0,
            AbsoluteLifetimeDays = 7
        }).Failed);
    }

    [Fact]
    public async Task RefreshSessionRotatesTokenCappedByAbsoluteExpiry()
    {
        var t0 = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        var now = t0.AddDays(28);
        var user = User.Create("owner@example.com", "hash", "Owner", null, UserStatus.Active, t0);
        var tokenService = new RefreshTokenService();
        var first = tokenService.Generate();
        var session = RefreshSession.Create(
            Guid.NewGuid(),
            user.Id,
            tenantId: null,
            tenantMembershipId: null,
            authenticationVersion: user.AuthenticationVersion,
            createdAt: t0,
            absoluteExpiresAt: t0.AddDays(30));
        var initialToken = RefreshSessionToken.CreateInitial(
            Guid.NewGuid(),
            session.Id,
            first.TokenHash,
            now.AddDays(-1),
            now.AddDays(6));

        var repo = new FakeRefreshSessionRepository(session, initialToken, user, [SystemRoles.SystemManager], observedUsedAt: null);
        var handler = new RefreshSessionCommandHandler(
            tokenService,
            repo,
            new FakeUnitOfWork(),
            new FakeJwtGenerator(now.AddMinutes(15)),
            Options.Create(new RefreshSessionOptions { IdleLifetimeDays = 7, AbsoluteLifetimeDays = 30 }),
            new FixedTimeProvider(now));

        var result = await handler.Handle(new RefreshSessionCommand(first.RawToken), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Payload.Session);
        Assert.NotNull(result.Value.RefreshCookie);
        Assert.Equal(session.AbsoluteExpiresAt, result.Value.RefreshCookie!.ExpiresAt);
        Assert.True(initialToken.IsUsed);
        Assert.NotNull(repo.AddedRotatedToken);
        Assert.Equal(2, repo.AddedRotatedToken!.Generation);
        Assert.Equal(initialToken.Id, repo.AddedRotatedToken.PredecessorTokenId);
    }

    [Fact]
    public async Task RefreshSessionRevokesSessionOnTokenReuseBeforeReturning401()
    {
        var t0 = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        var now = t0.AddHours(2);
        var user = User.Create("owner@example.com", "hash", "Owner", null, UserStatus.Active, t0);
        var tokenService = new RefreshTokenService();
        var first = tokenService.Generate();
        var session = RefreshSession.Create(
            Guid.NewGuid(),
            user.Id,
            null,
            null,
            user.AuthenticationVersion,
            t0,
            t0.AddDays(30));
        var usedToken = RefreshSessionToken.CreateInitial(Guid.NewGuid(), session.Id, first.TokenHash, t0, t0.AddDays(7));
        usedToken.MarkUsed(t0.AddHours(1));

        var repo = new FakeRefreshSessionRepository(session, usedToken, user, [SystemRoles.SystemManager], observedUsedAt: t0.AddHours(1));
        var uow = new FakeUnitOfWork();
        var handler = new RefreshSessionCommandHandler(
            tokenService,
            repo,
            uow,
            new FakeJwtGenerator(now.AddMinutes(15)),
            Options.Create(new RefreshSessionOptions()),
            new FixedTimeProvider(now));

        var result = await handler.Handle(new RefreshSessionCommand(first.RawToken), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.InvalidRefreshSession", result.Error.Code);
        Assert.True(session.IsRevoked);
        Assert.Equal(RefreshSession.RevokeReasonTokenReuse, session.RevocationReason);
        Assert.True(uow.SavedChangesCount > 0);
    }

    [Fact]
    public async Task RefreshSessionReturnsConflict409WhenConcurrentRequestRotatedFirst()
    {
        var t0 = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
        var now = t0.AddHours(2);
        var user = User.Create("owner@example.com", "hash", "Owner", null, UserStatus.Active, t0);
        var tokenService = new RefreshTokenService();
        var first = tokenService.Generate();
        var session = RefreshSession.Create(
            Guid.NewGuid(),
            user.Id,
            null,
            null,
            user.AuthenticationVersion,
            t0,
            t0.AddDays(30));
        var rotatedByWinner = RefreshSessionToken.CreateInitial(Guid.NewGuid(), session.Id, first.TokenHash, t0, t0.AddDays(7));
        rotatedByWinner.MarkUsed(now.AddMilliseconds(-10));

        // Pre-lock read saw unused (observedUsedAt: null), lock saw used -> 409 RefreshConflict, session NOT revoked
        var repo = new FakeRefreshSessionRepository(session, rotatedByWinner, user, [SystemRoles.SystemManager], observedUsedAt: null);
        var handler = new RefreshSessionCommandHandler(
            tokenService,
            repo,
            new FakeUnitOfWork(),
            new FakeJwtGenerator(now.AddMinutes(15)),
            Options.Create(new RefreshSessionOptions()),
            new FixedTimeProvider(now));

        var result = await handler.Handle(new RefreshSessionCommand(first.RawToken), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Authentication.RefreshConflict", result.Error.Code);
        Assert.False(session.IsRevoked);
    }

    private sealed class FakeRefreshSessionRepository(
        RefreshSession session,
        RefreshSessionToken token,
        User user,
        IReadOnlyCollection<string> systemRoles,
        DateTimeOffset? observedUsedAt) : IRefreshSessionRepository
    {
        public RefreshSessionToken? AddedRotatedToken { get; private set; }

        public Task<ObservedRefreshTokenLookup?> FindObservedByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult<ObservedRefreshTokenLookup?>(
                token.TokenHash == tokenHash
                    ? new ObservedRefreshTokenLookup(token.Id, session.Id, token.Generation, observedUsedAt, user.Id, session.TenantId, session.TenantMembershipId)
                    : null);

        public Task<LockedSessionIssueContext?> LockForSessionCreationAsync(Guid userId, Guid? tenantId, Guid? membershipId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LockedRefreshContext?> LockForRefreshAsync(ObservedRefreshTokenLookup observed, CancellationToken cancellationToken = default) =>
            Task.FromResult<LockedRefreshContext?>(new LockedRefreshContext(session, token, user, null, null, systemRoles));

        public Task<RefreshSession?> LockSessionForLogoutAsync(ObservedRefreshTokenLookup observed, CancellationToken cancellationToken = default) =>
            Task.FromResult<RefreshSession?>(session);

        public Task<User?> LockUserForUpdateAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);

        public Task<User?> LockUserByEmailForUpdateAsync(string email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> RevokeAllActiveByUserIdAsync(Guid userId, string reason, DateTimeOffset revokedAt, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public void AddSession(RefreshSession newSession, RefreshSessionToken initialToken)
        {
        }

        public void AddRotatedToken(RefreshSessionToken nextToken) => AddedRotatedToken = nextToken;
    }

    private sealed class FakeUnitOfWork : IIdentityUnitOfWork
    {
        public int SavedChangesCount { get; private set; }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
            await operation(cancellationToken);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SavedChangesCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeJwtGenerator(DateTimeOffset expiresAt) : IJwtTokenGenerator
    {
        public AccessTokenResult GenerateAccessToken(AccessTokenRequest request) =>
            new("new-access-jwt", expiresAt);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
