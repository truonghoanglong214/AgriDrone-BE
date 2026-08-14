using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Processing;

public sealed class AiProcessingJob : Entity
{
    private AiProcessingJob()
    {
    }

    public Guid MissionId { get; private set; }

    public Guid? ModelVersionId { get; private set; }

    public Guid? ThresholdProfileId { get; private set; }

    public AiJobType JobType { get; private set; }

    public AiJobStatus Status { get; private set; }

    public string? ExternalJobId { get; private set; }

    public JsonDocument Parameters { get; private set; } = null!;

    public int AttemptNumber { get; private set; }

    public decimal? ProgressPercent { get; private set; }

    public JsonDocument InputManifest { get; private set; } = null!;

    public JsonDocument OutputManifest { get; private set; } = null!;

    public string? ErrorCode { get; private set; }

    public Guid? ClientOperationId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTimeOffset QueuedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
}
