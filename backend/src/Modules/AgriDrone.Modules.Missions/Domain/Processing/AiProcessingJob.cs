using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
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

    public Guid? HarvestReadinessCriterionId { get; private set; }

    public int? HarvestReadinessCriterionVersionNumber { get; private set; }

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

    public DroneMission Mission { get; private set; } = null!;

    public AiModelVersion? ModelVersion { get; private set; }

    public AiThresholdProfile? ThresholdProfile { get; private set; }

    public ICollection<MissionPlantObservation> PlantObservations { get; private set; } = [];

    public void SnapshotHarvestReadinessCriterion(
        Guid criterionId,
        int criterionVersionNumber)
    {
        DomainGuard.NotEmpty(criterionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            criterionVersionNumber);

        if (Status != AiJobStatus.Queued)
        {
            throw new InvalidOperationException(
                "Harvest-readiness criterion can only be snapshotted while the job is queued.");
        }

        if (HarvestReadinessCriterionId.HasValue)
        {
            throw new InvalidOperationException(
                "Harvest-readiness criterion snapshot is already assigned.");
        }

        HarvestReadinessCriterionId = criterionId;
        HarvestReadinessCriterionVersionNumber = criterionVersionNumber;
    }
}
