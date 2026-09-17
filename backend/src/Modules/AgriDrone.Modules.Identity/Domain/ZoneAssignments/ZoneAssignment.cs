using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.ZoneAssignments;

public sealed class ZoneAssignment : Entity
{
    private ZoneAssignment()
    {
    }

    public Guid FarmMembershipId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid ZoneId { get; private set; }

    public Guid AssignedBy { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public FarmMembership FarmMembership { get; private set; } = null!;

    public User AssignedByUser { get; private set; } = null!;

    public static ZoneAssignment Create(
        Guid farmMembershipId,
        Guid farmId,
        Guid zoneId,
        Guid assignedBy,
        DateTimeOffset assignedAt)
    {
        DomainGuard.NotEmpty(farmMembershipId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.NotEmpty(assignedBy);
        DomainGuard.Utc(assignedAt);

        return new ZoneAssignment
        {
            Id = Guid.NewGuid(),
            FarmMembershipId = farmMembershipId,
            FarmId = farmId,
            ZoneId = zoneId,
            AssignedBy = assignedBy,
            AssignedAt = assignedAt
        };
    }

    public bool Revoke(DateTimeOffset revokedAt)
    {
        DomainGuard.Utc(revokedAt);

        if (revokedAt < AssignedAt)
        {
            throw new ArgumentException(
                "RevokedAt cannot be earlier than AssignedAt.",
                nameof(revokedAt));
        }

        if (RevokedAt.HasValue)
        {
            return false;
        }

        RevokedAt = revokedAt;
        return true;
    }
}
