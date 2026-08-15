using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed class AiThresholdProfile : Entity
{
    private AiThresholdProfile()
    {
    }

    public Guid ModelVersionId { get; private set; }

    public string ProfileName { get; private set; } = null!;

    public int VersionNumber { get; private set; }

    public ThresholdProfileStatus Status { get; private set; }

    public DateTimeOffset? EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public AiModelVersion ModelVersion { get; private set; } = null!;

    public ICollection<AiDetectionThreshold> DetectionThresholds { get; private set; } = [];

    public ICollection<AiProcessingJob> ProcessingJobs { get; private set; } = [];
}
