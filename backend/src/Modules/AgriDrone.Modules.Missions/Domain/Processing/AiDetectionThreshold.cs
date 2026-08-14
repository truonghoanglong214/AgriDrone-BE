using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed class AiDetectionThreshold : Entity
{
    private AiDetectionThreshold()
    {
    }

    public Guid ThresholdProfileId { get; private set; }

    public Guid ConditionId { get; private set; }

    public decimal MinConfidence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
