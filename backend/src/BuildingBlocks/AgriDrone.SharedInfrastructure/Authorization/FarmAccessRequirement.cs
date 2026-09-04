using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.SharedInfrastructure.Authorization;

internal sealed class FarmAccessRequirement(
    FarmAccessLevel requiredAccess) : IAuthorizationRequirement
{
    public FarmAccessLevel RequiredAccess { get; } = requiredAccess;
}
