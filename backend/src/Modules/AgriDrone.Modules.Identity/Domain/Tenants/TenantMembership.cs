using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Identity.Domain.Tenants;

public sealed class TenantMembership : Entity
{
    private TenantMembership(Guid id, Guid tenantId, Guid userId, TenantMemberRole role, GeneralStatus status, DateTimeOffset? joinedAt, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        Status = status;
        JoinedAt = joinedAt;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public TenantMemberRole Role { get; private set; }

    public GeneralStatus Status { get; private set; }

    public DateTimeOffset? JoinedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    public User User { get; private set; } = null!;

    public ICollection<FarmMembership> FarmMemberships { get; private set; } = [];

    public static TenantMembership Create(Guid tenantId, Guid userId, TenantMemberRole role, GeneralStatus status, DateTimeOffset? joinedAt, DateTimeOffset createAt)
    {
        return new TenantMembership(
            Guid.NewGuid(),
            tenantId,
            userId,
            role,
            status,
            joinedAt,
            createAt);
    }

    public void Activate(DateTimeOffset updateAt)
    {
        if (Status == GeneralStatus.Active)
            return;

        Status = GeneralStatus.Active;
    }

    public void Deactivate(DateTimeOffset updateAt)
    {
        if (Status == GeneralStatus.Inactive)
            return;

        if (Role == TenantMemberRole.Owner)
        {
            throw new InvalidOperationException(
                "The active OWNER membership cannot be deactivated.");
        }

        Status = GeneralStatus.Inactive;
    }

    public void ChangeRole(TenantMemberRole newRole)
    {
        if (Role == TenantMemberRole.Owner ||
            newRole == TenantMemberRole.Owner)
        {
            throw new InvalidOperationException(
                "OWNER role cannot be changed through the generic role update.");
        }

        if (newRole is not TenantMemberRole.Member and
            not TenantMemberRole.TenantAdmin)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newRole),
                newRole,
                "Only MEMBER and TENANT_ADMIN roles are supported.");
        }

        Role = newRole;
    }
}
