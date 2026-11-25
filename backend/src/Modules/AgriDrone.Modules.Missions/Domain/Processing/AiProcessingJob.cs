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

    public static AiProcessingJob RecordCompletedHealthRun(Guid missionId, Guid modelId,
        PlantHealthProcessingSnapshot snapshot, JsonDocument input, JsonDocument output,
        DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        DomainGuard.NotEmpty(missionId); DomainGuard.NotEmpty(modelId);
        ArgumentNullException.ThrowIfNull(snapshot);
        DomainGuard.Utc(startedAt); DomainGuard.Utc(completedAt);
        if (completedAt < startedAt) throw new ArgumentException("Completion cannot precede start.");
        return new AiProcessingJob
        {
            Id = Guid.CreateVersion7(), MissionId = missionId, ModelVersionId = modelId,
            JobType = AiJobType.DiseaseDetection, Status = AiJobStatus.Completed,
            Parameters = JsonSerializer.SerializeToDocument(snapshot), AttemptNumber = snapshot.SourceVersion,
            InputManifest = JsonDocument.Parse(input.RootElement.GetRawText()),
            OutputManifest = JsonDocument.Parse(output.RootElement.GetRawText()),
            QueuedAt = startedAt, StartedAt = startedAt, CompletedAt = completedAt, ProgressPercent = 100
        };
    }

    public bool HasCompleteComboInputCoverage()
    {
        var manifest = InputManifest.RootElement;
        if (manifest.ValueKind != JsonValueKind.Object ||
            !manifest.TryGetProperty("InputStorageKeys", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Array ||
            !manifest.TryGetProperty("image_outcomes", out var outcomes) ||
            outcomes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs.EnumerateArray())
        {
            if (input.ValueKind != JsonValueKind.String ||
                input.GetString() is not { Length: > 0 } key || !expected.Add(key))
            {
                return false;
            }
        }

        if (expected.Count == 0)
            return false;

        foreach (var outcome in outcomes.EnumerateArray())
        {
            if (outcome.ValueKind != JsonValueKind.Object ||
                !outcome.TryGetProperty("StorageKey", out var storageKey) ||
                storageKey.ValueKind != JsonValueKind.String ||
                storageKey.GetString() is not { } key || !expected.Remove(key) ||
                !outcome.TryGetProperty(nameof(Status), out var status) ||
                status.ValueKind != JsonValueKind.String || status.GetString() != "SUCCESS")
            {
                return false;
            }
        }

        return expected.Count == 0;
    }

    public static AiProcessingJob RecordCompletedComboRun(Guid missionId, Guid modelId,
        ComboAnalysisProcessingSnapshot snapshot, JsonDocument input, JsonDocument output,
        DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        DomainGuard.NotEmpty(missionId); DomainGuard.NotEmpty(modelId);
        ArgumentNullException.ThrowIfNull(snapshot);
        DomainGuard.Utc(startedAt); DomainGuard.Utc(completedAt);
        if (completedAt < startedAt) throw new ArgumentException("Completion cannot precede start.");
        return new AiProcessingJob
        {
            Id = Guid.CreateVersion7(), MissionId = missionId, ModelVersionId = modelId,
            JobType = AiJobType.DiseaseDetection, Status = AiJobStatus.Completed,
            Parameters = JsonSerializer.SerializeToDocument(snapshot), AttemptNumber = snapshot.SourceRevision,
            InputManifest = JsonDocument.Parse(input.RootElement.GetRawText()),
            OutputManifest = JsonDocument.Parse(output.RootElement.GetRawText()),
            QueuedAt = startedAt, StartedAt = startedAt, CompletedAt = completedAt, ProgressPercent = 100
        };
    }

    public static AiProcessingJob RecordCompletedMappingRun(
        Guid missionId,
        Guid modelId,
        MappingProcessingSnapshot snapshot,
        JsonDocument input,
        JsonDocument output,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        Guid clientOperationId)
    {
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(modelId);
        DomainGuard.NotEmpty(clientOperationId);
        ArgumentNullException.ThrowIfNull(snapshot);
        DomainGuard.Utc(startedAt);
        DomainGuard.Utc(completedAt);
        if (completedAt < startedAt) throw new ArgumentException("Completion cannot precede start.");
        if (snapshot.SourceVersion < 1 ||
            snapshot.GridBearingDeg is < 0 or >= 360 ||
            snapshot.RowSpacingM <= 0 ||
            snapshot.PlantSpacingM <= 0 ||
            string.IsNullOrWhiteSpace(snapshot.AlgorithmVersion) ||
            !IsSha256(snapshot.InputFingerprint) ||
            !IsSha256(snapshot.OutputFingerprint))
        {
            throw new ArgumentException("Mapping processing snapshot is incomplete or invalid.", nameof(snapshot));
        }

        return new AiProcessingJob
        {
            Id = Guid.CreateVersion7(),
            MissionId = missionId,
            ModelVersionId = modelId,
            JobType = AiJobType.Mapping,
            Status = AiJobStatus.Completed,
            Parameters = JsonSerializer.SerializeToDocument(snapshot),
            AttemptNumber = snapshot.SourceVersion,
            InputManifest = JsonDocument.Parse(input.RootElement.GetRawText()),
            OutputManifest = JsonDocument.Parse(output.RootElement.GetRawText()),
            ClientOperationId = clientOperationId,
            QueuedAt = startedAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ProgressPercent = 100
        };
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

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
