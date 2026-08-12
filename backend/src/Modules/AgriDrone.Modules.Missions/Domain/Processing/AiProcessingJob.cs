using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed class AiProcessingJob : Entity
{
    private AiProcessingJob()
    {
    }

    public Guid MissionId { get; private set; }

    public AiJobType JobType { get; private set; }

    public AiJobStatus Status { get; private set; }

    public string? ExternalJobId { get; private set; }

    public JsonDocument Parameters { get; private set; } = null!;

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
}
