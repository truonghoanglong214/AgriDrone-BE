using AgriDrone.Modules.Harvests.Domain.Quality;

namespace AgriDrone.Modules.Harvests.Domain.PlantHarvests;

public sealed class PlantHarvestQualityDetail
{
    private PlantHarvestQualityDetail()
    {
    }

    public Guid PlantHarvestRecordId { get; private set; }

    public Guid QualityGradeId { get; private set; }

    public Guid FarmId { get; private set; }

    public int FruitCount { get; private set; }

    public decimal? WeightKg { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public PlantHarvestRecord PlantHarvestRecord { get; private set; } = null!;

    public HarvestQualityGrade QualityGrade { get; private set; } = null!;
}
