using AgriDrone.Modules.Identity.Domain.Tenants;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class TenantRoleRequirement(
    params TenantMemberRole[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlySet<TenantMemberRole> AllowedRoles { get; } =
        allowedRoles.ToHashSet();
}
