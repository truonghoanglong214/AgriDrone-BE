using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class FarmRoleRequirement(FarmAccessLevel requiredAccess)
    : IAuthorizationRequirement
{
    public FarmAccessLevel RequiredAccess { get; } = requiredAccess;
}
