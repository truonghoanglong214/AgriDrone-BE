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

    public static Role CreateSystemRole(
        string code,
        string name,
        string description,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        DomainGuard.Utc(createdAt);

        return new Role
        {
            Id = Guid.NewGuid(),
            Code = code.Trim(),
            Name = name.Trim(),
            Description = description.Trim(),
            CreatedAt = createdAt
        };
    }

    public void UpdateDefinition(string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        Description = description.Trim();
    }
}
