using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Roles;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.ZoneAssignments;
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

    public ICollection<TenantMembership> TenantMemberships { get; private set; } = [];

    public ICollection<FarmMembership> FarmMemberships { get; private set; } = [];

    public ICollection<UserRole> UserRoles { get; private set; } = [];

    public ICollection<ZoneAssignment> AssignedZoneAssignments { get; private set; } = [];

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

    public void UpdateProfile(
    string fullName,
    string? phone,
    DateTimeOffset updatedAt)
    {
        FullName = fullName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone)
            ? null
            : phone.Trim();

        UpdatedAt = updatedAt;
    }

    public void Activate(DateTimeOffset updatedAt)
    {
        if (Status == UserStatus.Active)
            return;

        Status = UserStatus.Active;
        UpdatedAt = updatedAt;
    }

    public void Inactive(DateTimeOffset updatedAt)
    {
        if (Status == UserStatus.Inactive)
            return;

        Status = UserStatus.Inactive;
        UpdatedAt = updatedAt;
    }

    public void ChangePassword(
    string passwordHash,
    DateTimeOffset updatedAt)
    {

        PasswordHash = passwordHash;
        UpdatedAt = updatedAt;
    }
}
