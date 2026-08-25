using AgriDrone.Modules.Identity.Domain.Users;

namespace AgriDrone.Modules.Identity.Domain.Roles;

public sealed class UserRole
{
    private UserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public User User { get; private set; } = null!;

    public Role Role { get; private set; } = null!;

    internal static UserRole Create(
        Guid userId,
        Guid roleId,
        DateTimeOffset assignedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(roleId, Guid.Empty);

        return new UserRole
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = assignedAt
        };
    }
}
