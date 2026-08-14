using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.FarmMemberships;

public sealed class FarmMembership : Entity
{
    private FarmMembership()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid UserId { get; private set; }

    public FarmMemberRole Role { get; private set; }

    public FarmAccessScope AccessScope { get; private set; }

    public GeneralStatus Status { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
