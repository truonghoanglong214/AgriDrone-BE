using AgriDrone.Modules.Plants.Domain.Verifications;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Conditions;

public sealed class PlantCondition : Entity
{
    private PlantCondition()
    {
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? ScientificName { get; private set; }

    public ConditionType ConditionType { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<ConditionDetection> Detections { get; private set; } = [];

    public ICollection<ConditionDetectionReview> CorrectedReviews { get; private set; } = [];
}
