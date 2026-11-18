using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed class AiModelVersion : Entity
{
    private AiModelVersion()
    {
    }

    public string ModelName { get; private set; } = null!;

    public string Version { get; private set; } = null!;

    public AiModelType ModelType { get; private set; }

    public string? ArtifactUri { get; private set; }

    public JsonDocument Metrics { get; private set; } = null!;

    public DateTimeOffset? TrainedAt { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<AiThresholdProfile> ThresholdProfiles { get; private set; } = [];

    public ICollection<AiProcessingJob> ProcessingJobs { get; private set; } = [];

    public ICollection<MissionPlantObservation> PlantObservations { get; private set; } = [];

    public static AiModelVersion CreateMappingModel(
        Guid id,
        string modelName,
        string version,
        DateTimeOffset now)
    {
        DomainGuard.NotEmpty(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        DomainGuard.Utc(now);

        return new AiModelVersion
        {
            Id = id,
            ModelName = modelName.Trim(),
            Version = version.Trim(),
            ModelType = AiModelType.PlantDetection,
            Metrics = JsonDocument.Parse("{}"),
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
