using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Users;

public sealed class User : AggregateRoot
{
    private User()
    {
    }

    public string Email { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string? Phone { get; private set; }

    public UserStatus Status { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }
}
