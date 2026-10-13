using AgriDrone.SharedKernel.Application.Abstractions.Authorization;

namespace AgriDrone.Modules.Identity.Domain.Roles;

public static class SystemRoles
{
    public const string SystemAdmin = SystemRoleCodes.SystemAdmin;

    public const string SystemManager = SystemRoleCodes.SystemManager;

    public static readonly IReadOnlyCollection<SystemRoleDefinition> All =
    [
        new(
            SystemAdmin,
            "System Administrator",
            "Administrator with system-wide access."),
        new(
            SystemManager,
            "System Manager",
            "AgriDrone operations manager assigned to customer farms.")
    ];
}

public sealed record SystemRoleDefinition(
    string Code,
    string Name,
    string Description);
