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

    public static FarmMembership Create(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        FarmMemberRole role,
        FarmAccessScope accessScope,
        IReadOnlyCollection<Guid> zoneIds,
        Guid assignedBy,
        DateTimeOffset createdAt)
    {
        var membership = Create(
            tenantId,
            farmId,
            userId,
            role,
            accessScope,
            createdAt);

        ValidateZones(accessScope, zoneIds);
        membership.SynchronizeZones(zoneIds, assignedBy, createdAt);

        return membership;
    }

    public bool Assign(
        FarmMemberRole role,
        FarmAccessScope accessScope,
        IReadOnlyCollection<Guid> zoneIds,
        Guid assignedBy,
        DateTimeOffset assignedAt)
    {
        DomainGuard.NotEmpty(assignedBy);
        DomainGuard.Utc(assignedAt);
        ValidateRole(role);
        ValidateAccessScope(accessScope);
        ValidateZones(accessScope, zoneIds);

        var membershipChanged =
            Role != role ||
            AccessScope != accessScope ||
            Status != GeneralStatus.Active;
        var zonesChanged = !HasSameActiveZones(zoneIds);

        if (!membershipChanged && !zonesChanged)
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

        SynchronizeZones(zoneIds, assignedBy, assignedAt);
        Version++;

        return true;
    }

    public bool Matches(
        FarmMemberRole role,
        FarmAccessScope accessScope,
        IReadOnlyCollection<Guid> zoneIds)
    {
        ValidateRole(role);
        ValidateAccessScope(accessScope);
        ValidateZones(accessScope, zoneIds);

        return Role == role &&
            AccessScope == accessScope &&
            Status == GeneralStatus.Active &&
            HasSameActiveZones(zoneIds);
    }

    public bool Deactivate(DateTimeOffset deactivatedAt)
    {
        DomainGuard.Utc(deactivatedAt);

        if (Status == GeneralStatus.Inactive)
        {
            return false;
        }

        foreach (var zoneAssignment in ZoneAssignments.Where(
                     assignment => assignment.RevokedAt is null))
        {
            zoneAssignment.Revoke(deactivatedAt);
        }

        Status = GeneralStatus.Inactive;
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

    private static void ValidateZones(
        FarmAccessScope accessScope,
        IReadOnlyCollection<Guid> zoneIds)
    {
        ArgumentNullException.ThrowIfNull(zoneIds);

        if (accessScope == FarmAccessScope.AllZones && zoneIds.Count != 0)
        {
            throw new ArgumentException(
                "Zone IDs must be empty for ALL_ZONES access.",
                nameof(zoneIds));
        }

        if (accessScope == FarmAccessScope.SelectedZones &&
            (zoneIds.Count == 0 ||
             zoneIds.Any(zoneId => zoneId == Guid.Empty) ||
             zoneIds.Distinct().Count() != zoneIds.Count))
        {
            throw new ArgumentException(
                "Selected zone IDs must be non-empty and distinct.",
                nameof(zoneIds));
        }
    }

    private bool HasSameActiveZones(IReadOnlyCollection<Guid> zoneIds)
    {
        var requestedZoneIds = AccessScope == FarmAccessScope.AllZones
            ? Array.Empty<Guid>()
            : zoneIds;
        var activeZoneIds = ZoneAssignments
            .Where(assignment => assignment.RevokedAt is null)
            .Select(assignment => assignment.ZoneId)
            .ToHashSet();

        return activeZoneIds.SetEquals(requestedZoneIds);
    }

    private void SynchronizeZones(
        IReadOnlyCollection<Guid> zoneIds,
        Guid assignedBy,
        DateTimeOffset assignedAt)
    {
        var requestedZoneIds = AccessScope == FarmAccessScope.AllZones
            ? new HashSet<Guid>()
            : zoneIds.ToHashSet();
        var activeAssignments = ZoneAssignments
            .Where(assignment => assignment.RevokedAt is null)
            .ToArray();

        foreach (var assignment in activeAssignments.Where(
                     assignment => !requestedZoneIds.Contains(assignment.ZoneId)))
        {
            assignment.Revoke(assignedAt);
        }

        var activeZoneIds = activeAssignments
            .Where(assignment => assignment.RevokedAt is null)
            .Select(assignment => assignment.ZoneId)
            .ToHashSet();

        foreach (var zoneId in requestedZoneIds.Where(
                     zoneId => !activeZoneIds.Contains(zoneId)))
        {
            ZoneAssignments.Add(ZoneAssignment.Create(
                Id,
                FarmId,
                zoneId,
                assignedBy,
                assignedAt));
        }
    }
}
