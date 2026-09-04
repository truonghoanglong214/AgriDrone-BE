using AgriDrone.SharedKernel.Application.Abstractions.Authorization;

namespace AgriDrone.Modules.Identity.Domain.Roles;

public static class SystemRoles
{
    public const string SystemAdmin = SystemRoleCodes.SystemAdmin;

    public static readonly IReadOnlyCollection<SystemRoleDefinition> All =
    [
        new(
            SystemAdmin,
            "System Administrator",
            "Administrator with system-wide access.")
    ];
}

public sealed record SystemRoleDefinition(
    string Code,
    string Name,
    string Description);
