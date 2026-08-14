using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class FarmRoleRequirement(
    params FarmMemberRole[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlySet<FarmMemberRole> AllowedRoles { get; } =
        allowedRoles.ToHashSet();
}
