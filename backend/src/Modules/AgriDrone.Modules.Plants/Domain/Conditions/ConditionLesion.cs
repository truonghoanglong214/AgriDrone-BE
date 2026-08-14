using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Plants.Domain.Conditions;

public sealed class ConditionLesion : Entity
{
    private ConditionLesion()
    {
    }

    public Guid ConditionDetectionId { get; private set; }

    public Guid MediaId { get; private set; }

    public decimal XMin { get; private set; }

    public decimal YMin { get; private set; }

    public decimal XMax { get; private set; }

    public decimal YMax { get; private set; }

    public decimal? Confidence { get; private set; }

    public decimal? AffectedRatio { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
