using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Zones;

public sealed class FarmZonePlantMembership : Entity
{
    private FarmZonePlantMembership() { }

    public Guid FarmId { get; private set; }
    public Guid ZoneId { get; private set; }
    public Guid PlantId { get; private set; }
    public FarmZonePlantMembershipSource Source { get; private set; }
    public bool IsAssigned { get; private set; }
    public Guid AssignedBy { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public uint Version { get; private set; }

    public static FarmZonePlantMembership Create(
        Guid farmId,
        Guid zoneId,
        Guid plantId,
        FarmZonePlantMembershipSource source,
        Guid assignedBy,
        string? reason,
        DateTimeOffset effectiveFrom,
        bool isAssigned = true)
    {
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.NotEmpty(plantId);
        DomainGuard.NotEmpty(assignedBy);
        DomainGuard.Utc(effectiveFrom);

        return new FarmZonePlantMembership
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            ZoneId = zoneId,
            PlantId = plantId,
            Source = source,
            IsAssigned = isAssigned,
            AssignedBy = assignedBy,
            Reason = reason?.Trim(),
            EffectiveFrom = effectiveFrom
        };
    }

    public void Close(DateTimeOffset effectiveTo, string? reason)
    {
        DomainGuard.Utc(effectiveTo);
        if (EffectiveTo.HasValue) return;
        ArgumentOutOfRangeException.ThrowIfLessThan(effectiveTo, EffectiveFrom);

        EffectiveTo = effectiveTo;
        Reason = string.IsNullOrWhiteSpace(reason) ? Reason : reason.Trim();
        Version++;
    }
}
