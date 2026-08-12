using System.Text.Json;
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
}
