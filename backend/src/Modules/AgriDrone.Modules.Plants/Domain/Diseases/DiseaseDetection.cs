using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Diseases;

public sealed class DiseaseDetection : Entity
{
    private DiseaseDetection()
    {
    }

    public Guid PlantScanId { get; private set; }

    public Guid DiseaseId { get; private set; }

    public Guid? ModelVersionId { get; private set; }

    public FindingSource Source { get; private set; }

    public decimal? Confidence { get; private set; }

    public DiseaseSeverity Severity { get; private set; }

    public int LesionCount { get; private set; }

    public decimal? AffectedRatio { get; private set; }

    public ReviewStatus ReviewStatus { get; private set; }

    public Guid? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
