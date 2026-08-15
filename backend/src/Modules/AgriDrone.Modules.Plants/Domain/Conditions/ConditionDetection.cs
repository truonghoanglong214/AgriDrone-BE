using AgriDrone.Modules.Plants.Domain.Diseases;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Scans;
using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Conditions;

public sealed class ConditionDetection : Entity
{
    private ConditionDetection()
    {
    }

    public Guid PlantScanId { get; private set; }

    public Guid ConditionId { get; private set; }

    public Guid? ModelVersionId { get; private set; }

    public FindingSource Source { get; private set; }

    public decimal? Confidence { get; private set; }

    public Guid SeverityLevelId { get; private set; }

    public decimal? ThresholdUsed { get; private set; }

    public int LesionCount { get; private set; }

    public decimal? AffectedRatio { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public PlantScan PlantScan { get; private set; } = null!;

    public PlantCondition Condition { get; private set; } = null!;

    public HealthLevel SeverityLevel { get; private set; } = null!;

    public ICollection<ConditionLesion> Lesions { get; private set; } = [];

    public ICollection<ConditionDetectionReview> Reviews { get; private set; } = [];
}
