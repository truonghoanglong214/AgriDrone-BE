using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AgriDrone.SharedInfrastructure.Authorization;

public static class DependencyInjection
{
    public static IServiceCollection AddAccessAuthorization(
        this IServiceCollection services)
    {
        services.AddScoped<
            IAuthorizationHandler,
            TenantAccessAuthorizationHandler>();
        services.AddScoped<
            IAuthorizationHandler,
            FarmAccessAuthorizationHandler>();
        services.AddScoped<
            IAuthorizationHandler,
            ZoneAccessAuthorizationHandler>();

        services.AddAuthorization(authorization =>
        {
            authorization.AddPolicy(
                AccessAuthorizationPolicies.SystemAdmin,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireRole(SystemRoleCodes.SystemAdmin));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.TenantMember,
                policy => policy.RequireTenantAccess(
                    TenantAccessLevel.Member));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.TenantAdmin,
                policy => policy.RequireTenantAccess(
                    TenantAccessLevel.Admin));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.TenantOwner,
                policy => policy.RequireTenantAccess(
                    TenantAccessLevel.Owner));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.FarmRead,
                policy => policy.RequireFarmAccess(
                    FarmAccessLevel.Member));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.FarmManage,
                policy => policy.RequireFarmAccess(
                    FarmAccessLevel.Manager));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.ZoneRead,
                policy => policy.RequireZoneAccess(
                    FarmAccessLevel.Member));

            authorization.AddPolicy(
                AccessAuthorizationPolicies.ZoneManage,
                policy => policy.RequireZoneAccess(
                    FarmAccessLevel.Manager));
        });

        return services;
    }
}
