using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Users;

public sealed class User : AggregateRoot
{
    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string passwordHash,
        string fullName,
        string? phone,
        UserStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Phone = phone;
        Status = status;
        CreatedAt = createdAt;
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

    public static User Create(
        string email,
        string passwordHash,
        string fullName,
        string? phone,
        UserStatus userStatus,
        DateTimeOffset createAt)
    {
        return new User(
            Guid.NewGuid(),
            email,
            passwordHash,
            fullName,
            phone,
            userStatus,
            createAt);
    }
}
