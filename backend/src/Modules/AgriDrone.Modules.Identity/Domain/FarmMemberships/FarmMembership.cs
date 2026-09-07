using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Domain.ZoneAssignments;
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

    public long Version { get; private set; } = 1;

    public User User { get; private set; } = null!;

    public TenantMembership TenantMembership { get; private set; } = null!;

    public ICollection<ZoneAssignment> ZoneAssignments { get; private set; } = [];

    public static FarmMembership Create(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        FarmMemberRole role,
        FarmAccessScope accessScope,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(userId);
        DomainGuard.Utc(createdAt);
        ValidateRole(role);
        ValidateAccessScope(accessScope);

        return new FarmMembership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            UserId = userId,
            Role = role,
            AccessScope = accessScope,
            Status = GeneralStatus.Active,
            JoinedAt = createdAt,
            CreatedAt = createdAt
        };
    }

    public bool Assign(
        FarmMemberRole role,
        FarmAccessScope accessScope,
        DateTimeOffset assignedAt)
    {
        DomainGuard.Utc(assignedAt);
        ValidateRole(role);
        ValidateAccessScope(accessScope);

        if (Role == role &&
            AccessScope == accessScope &&
            Status == GeneralStatus.Active)
        {
            return false;
        }

        Role = role;
        AccessScope = accessScope;

        if (Status != GeneralStatus.Active)
        {
            Status = GeneralStatus.Active;
            JoinedAt = assignedAt;
        }

        Version++;

        return true;
    }

    private static void ValidateRole(FarmMemberRole role)
    {
        if (role is not FarmMemberRole.Manager and not FarmMemberRole.Worker)
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }
    }

    private static void ValidateAccessScope(FarmAccessScope accessScope)
    {
        if (accessScope is not FarmAccessScope.AllZones and
            not FarmAccessScope.SelectedZones)
        {
            throw new ArgumentOutOfRangeException(nameof(accessScope));
        }
    }
}
