using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Harvests.Domain.HarvestBatches;

public sealed class HarvestBatch : AggregateRoot
{
    private HarvestBatch()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid SeasonId { get; private set; }

    public Guid? ZoneId { get; private set; }

    public string BatchCode { get; private set; } = null!;

    public DateTimeOffset HarvestedAt { get; private set; }

    public int? ReportedFruitCount { get; private set; }

    public decimal? ReportedWeightKg { get; private set; }

    public string? Notes { get; private set; }

    public HarvestBatchStatus Status { get; private set; }

    public Guid? CompletedBy { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public uint Version { get; private set; }
}
