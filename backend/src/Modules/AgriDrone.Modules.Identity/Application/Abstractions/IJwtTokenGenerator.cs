using System;
using System.Collections.Generic;
using System.Text;
using AgriDrone.Modules.Identity.Domain.Tenants;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    public sealed record AccessTokenResult(
        string AccessToken,
        DateTimeOffset ExpiresAt);

    public sealed record AccessTokenRequest(
        Guid UserId,
        string Email,
        Guid? TenantId,
        Guid? TenantMembershipId,
        TenantMemberRole? TenantRole,
        IReadOnlyCollection<string> SystemRoles);

    public interface IJwtTokenGenerator
    {
        AccessTokenResult GenerateAccessToken(
            AccessTokenRequest request);
    }
}
