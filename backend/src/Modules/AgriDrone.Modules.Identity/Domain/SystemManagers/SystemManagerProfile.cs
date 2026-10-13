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

public enum FlightQualificationStatus
{
    Pending,
    Qualified,
    Suspended,
    Revoked
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
        QualificationStatus = FlightQualificationStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public SystemManagerProfileStatus Status { get; private set; }

    public SystemManagerAvailabilityStatus Availability { get; private set; }

    public FlightQualificationStatus QualificationStatus { get; private set; }

    public DateTimeOffset? QualificationExpiresAt { get; private set; }

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

    public void UpdateQualification(
        FlightQualificationStatus status,
        DateTimeOffset? expiresAt,
        DateTimeOffset updatedAt,
        long expectedVersion)
    {
        EnsureVersion(expectedVersion);
        DomainGuard.Utc(updatedAt);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (expiresAt.HasValue)
        {
            DomainGuard.Utc(expiresAt.Value);
        }

        if (status == FlightQualificationStatus.Qualified &&
            (!expiresAt.HasValue || expiresAt.Value <= updatedAt))
        {
            throw new ArgumentException(
                "A qualified manager requires a future qualification expiry.",
                nameof(expiresAt));
        }

        QualificationStatus = status;
        QualificationExpiresAt = expiresAt;

        if (status != FlightQualificationStatus.Qualified)
        {
            Availability = SystemManagerAvailabilityStatus.Unavailable;
        }

        Touch(updatedAt);
    }

    public bool CanBeAssigned(DateTimeOffset now) =>
        Status == SystemManagerProfileStatus.Active &&
        Availability == SystemManagerAvailabilityStatus.Available &&
        HasCurrentQualification(now);

    public bool CanOperate(DateTimeOffset now) =>
        Status == SystemManagerProfileStatus.Active &&
        HasCurrentQualification(now);

    private bool HasCurrentQualification(DateTimeOffset now) =>
        QualificationStatus == FlightQualificationStatus.Qualified &&
        QualificationExpiresAt is DateTimeOffset expiresAt &&
        expiresAt > now;

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
