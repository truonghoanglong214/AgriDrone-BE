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
}
