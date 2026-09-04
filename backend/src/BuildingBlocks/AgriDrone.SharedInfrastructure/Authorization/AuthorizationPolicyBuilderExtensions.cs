using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.SharedInfrastructure.Authorization;

public static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireTenantAccess(
        this AuthorizationPolicyBuilder policy,
        TenantAccessLevel requiredAccess)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!Enum.IsDefined(requiredAccess))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredAccess),
                requiredAccess,
                "The requested tenant access level is not supported.");
        }

        return policy
            .RequireAuthenticatedUser()
            .AddRequirements(new TenantAccessRequirement(requiredAccess));
    }

    public static AuthorizationPolicyBuilder RequireFarmAccess(
        this AuthorizationPolicyBuilder policy,
        FarmAccessLevel requiredAccess)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!Enum.IsDefined(requiredAccess))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredAccess),
                requiredAccess,
                "The requested farm access level is not supported.");
        }

        return policy
            .RequireAuthenticatedUser()
            .AddRequirements(new FarmAccessRequirement(requiredAccess));
    }

    public static AuthorizationPolicyBuilder RequireZoneAccess(
        this AuthorizationPolicyBuilder policy,
        FarmAccessLevel requiredAccess)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!Enum.IsDefined(requiredAccess))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredAccess),
                requiredAccess,
                "The requested zone access level is not supported.");
        }

        return policy
            .RequireAuthenticatedUser()
            .AddRequirements(new ZoneAccessRequirement(requiredAccess));
    }
}
