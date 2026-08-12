using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Harvests.Domain.PlantHarvests;

public sealed class PlantHarvestRecord : Entity
{
    private PlantHarvestRecord()
    {
    }

    public Guid FarmId { get; private set; }

    public Guid HarvestBatchId { get; private set; }

    public Guid PlantId { get; private set; }

    public int FruitCount { get; private set; }

    public decimal WeightKg { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
