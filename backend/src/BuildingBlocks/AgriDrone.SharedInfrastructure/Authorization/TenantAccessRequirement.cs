using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.SharedInfrastructure.Authorization;

internal sealed class TenantAccessRequirement(
    TenantAccessLevel requiredAccess) : IAuthorizationRequirement
{
    public TenantAccessLevel RequiredAccess { get; } = requiredAccess;
}
