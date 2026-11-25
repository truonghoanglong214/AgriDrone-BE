using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.Modules.Identity.Application.Options;
using AgriDrone.Modules.Identity.Domain.RefreshSessions;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using DomainRefreshSession = AgriDrone.Modules.Identity.Domain.RefreshSessions.RefreshSession;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser;

internal static class AuthenticationSessionFactory
{
    public static AuthenticationSessionResponse Create(
        IJwtTokenGenerator jwtTokenGenerator,
        User user,
        TenantMembership membership,
        IReadOnlyCollection<string> systemRoles) =>
        Create(jwtTokenGenerator, user, membership, membership.Tenant, systemRoles);

    public static AuthenticationSessionResponse Create(
        IJwtTokenGenerator jwtTokenGenerator,
        User user,
        TenantMembership membership,
        Tenant tenant,
        IReadOnlyCollection<string> systemRoles)
    {
        var token = jwtTokenGenerator.GenerateAccessToken(
            new AccessTokenRequest(
                user.Id,
                user.Email,
                membership.TenantId,
                membership.Id,
                membership.Role,
                systemRoles));

        return new AuthenticationSessionResponse(
            token.AccessToken,
            token.ExpiresAt,
            ToTenantOption(membership, tenant));
    }

    public static AuthenticationSessionResponse CreateSystemSession(
        IJwtTokenGenerator jwtTokenGenerator,
        User user,
        IReadOnlyCollection<string> systemRoles)
    {
        var token = jwtTokenGenerator.GenerateAccessToken(
            new AccessTokenRequest(
                user.Id,
                user.Email,
                null,
                null,
                null,
                systemRoles));

        return new AuthenticationSessionResponse(
            token.AccessToken,
            token.ExpiresAt,
            null);
    }

    public static TenantOptionResponse ToTenantOption(TenantMembership membership) =>
        ToTenantOption(membership, membership.Tenant);

    public static TenantOptionResponse ToTenantOption(TenantMembership membership, Tenant tenant)
    {
        return new TenantOptionResponse(
            membership.TenantId,
            tenant.Code,
            tenant.Name,
            membership.Role);
    }

    public static IssuedRefreshCookie IssueInitialRefreshSession(
        IRefreshSessionRepository refreshSessionRepository,
        IRefreshTokenService refreshTokenService,
        RefreshSessionOptions options,
        User user,
        Tenant? tenant,
        TenantMembership? membership,
        DateTimeOffset now)
    {
        var absoluteExpiresAt = now.AddDays(options.AbsoluteLifetimeDays);
        var slidingExpiresAt = now.AddDays(options.IdleLifetimeDays);
        var tokenExpiresAt = slidingExpiresAt < absoluteExpiresAt ? slidingExpiresAt : absoluteExpiresAt;

        var sessionId = Guid.CreateVersion7();
        var session = DomainRefreshSession.Create(
            sessionId,
            user.Id,
            tenant?.Id,
            membership?.Id,
            user.AuthenticationVersion,
            now,
            absoluteExpiresAt);

        var generated = refreshTokenService.Generate();
        var initialToken = RefreshSessionToken.CreateInitial(
            Guid.CreateVersion7(),
            sessionId,
            generated.TokenHash,
            now,
            tokenExpiresAt);

        refreshSessionRepository.AddSession(session, initialToken);
        return new IssuedRefreshCookie(generated.RawToken, tokenExpiresAt);
    }
}
