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
}
