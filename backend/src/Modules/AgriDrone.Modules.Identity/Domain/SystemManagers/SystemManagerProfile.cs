using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.SystemManagers;

public enum SystemManagerProfileStatus
{
    Active,
    Suspended
}

public enum SystemManagerAvailabilityStatus
{
    Available,
    Unavailable
}

public sealed class SystemManagerProfile : AggregateRoot
{
    private SystemManagerProfile()
    {
    }

    private SystemManagerProfile(Guid userId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Status = SystemManagerProfileStatus.Suspended;
        Availability = SystemManagerAvailabilityStatus.Unavailable;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public SystemManagerProfileStatus Status { get; private set; }

    public SystemManagerAvailabilityStatus Availability { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Version { get; private set; } = 1;

    public User User { get; private set; } = null!;

    public ICollection<FarmManagerAssignment> FarmAssignments { get; private set; } = [];

    public static SystemManagerProfile Create(
        Guid userId,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(userId);
        DomainGuard.Utc(createdAt);
        return new SystemManagerProfile(userId, createdAt);
    }

    public void Activate(DateTimeOffset updatedAt, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.Utc(updatedAt);

        if (Status == SystemManagerProfileStatus.Active)
        {
            return;
        }

        Status = SystemManagerProfileStatus.Active;
        Touch(updatedAt);
    }

    public void Suspend(DateTimeOffset updatedAt, long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.Utc(updatedAt);

        if (Status == SystemManagerProfileStatus.Suspended &&
            Availability == SystemManagerAvailabilityStatus.Unavailable)
        {
            return;
        }

        Status = SystemManagerProfileStatus.Suspended;
        Availability = SystemManagerAvailabilityStatus.Unavailable;
        Touch(updatedAt);
    }

    public void UpdateAvailability(
        SystemManagerAvailabilityStatus availability,
        DateTimeOffset updatedAt,
        long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.Utc(updatedAt);

        if (!Enum.IsDefined(availability))
        {
            throw new ArgumentOutOfRangeException(nameof(availability));
        }

        if (Availability == availability)
        {
            return;
        }

        Availability = availability;
        Touch(updatedAt);
    }

    public bool CanBeAssigned(DateTimeOffset now) =>
        Status == SystemManagerProfileStatus.Active &&
        Availability == SystemManagerAvailabilityStatus.Available;

    public bool CanOperate(DateTimeOffset now) =>
        Status == SystemManagerProfileStatus.Active;

    private void EnsureVersion(long expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new InvalidOperationException(
                $"System manager profile version conflict. Expected {expectedVersion}, current {Version}.");
        }
    }

    private void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Version++;
    }
}
