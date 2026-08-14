using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Tenants;

public sealed class TenantMembership : Entity
{
    private TenantMembership()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantMemberRole Role { get; private set; }

    public GeneralStatus Status { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
