using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Farms.Domain.Maps;

public sealed class FarmBaselinePointer : AggregateRoot
{
    private FarmBaselinePointer() { }

    public Guid FarmId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CurrentBaselineMapVersionId { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public static FarmBaselinePointer Create(
        Guid tenantId,
        Guid farmId,
        Guid baselineMapVersionId,
        Guid actorId,
        DateTimeOffset at)
    {
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(baselineMapVersionId, nameof(baselineMapVersionId));
        DomainGuard.NotEmpty(actorId, nameof(actorId));
        DomainGuard.Utc(at, nameof(at));

        return new FarmBaselinePointer
        {
            Id = farmId,
            FarmId = farmId,
            TenantId = tenantId,
            CurrentBaselineMapVersionId = baselineMapVersionId,
            UpdatedBy = actorId,
            UpdatedAt = at
        };
    }

}
