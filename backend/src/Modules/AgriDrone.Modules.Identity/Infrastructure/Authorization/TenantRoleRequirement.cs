using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class TenantRoleRequirement(TenantAccessLevel requiredAccess)
    : IAuthorizationRequirement
{
    public TenantAccessLevel RequiredAccess { get; } = requiredAccess;
}
