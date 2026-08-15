using AgriDrone.Modules.Harvests.Domain.PlantHarvests;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Harvests.Domain.Quality;

public sealed class HarvestQualityGrade : Entity
{
    private HarvestQualityGrade()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<PlantHarvestQualityDetail> PlantHarvestQualityDetails { get; private set; } = [];
}
