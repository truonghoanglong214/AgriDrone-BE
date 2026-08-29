using AgriDrone.Modules.Identity.Application.Contracts.Authentication;
using AgriDrone.Modules.Identity.Application.Abstractions.Services;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;

namespace AgriDrone.Modules.Identity.Application.Features.LoginUser;

internal static class AuthenticationSessionFactory
{
    public static AuthenticationSessionResponse Create(
        IJwtTokenGenerator jwtTokenGenerator,
        User user,
        TenantMembership membership,
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
            ToTenantOption(membership));
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

    public static TenantOptionResponse ToTenantOption(
        TenantMembership membership)
    {
        return new TenantOptionResponse(
            membership.TenantId,
            membership.Tenant.Code,
            membership.Tenant.Name,
            membership.Role);
    }
}
