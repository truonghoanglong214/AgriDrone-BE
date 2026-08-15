using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Roles;

public sealed class Role : Entity
{
    private Role()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public ICollection<UserRole> UserRoles { get; private set; } = [];
}
