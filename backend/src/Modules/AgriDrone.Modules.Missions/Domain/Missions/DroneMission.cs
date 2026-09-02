using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.SharedKernel.Domain;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public sealed class DroneMission : AggregateRoot
{
    private DroneMission()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid FarmId { get; private set; }

    public Guid ZoneId { get; private set; }

    public Guid? SourceMapVersionId { get; private set; }

    public Guid? PreflightConfirmedBy { get; private set; }

    public DateTimeOffset? PreflightConfirmedAt { get; private set; }

    public uint Version { get; private set; }

    public Guid DroneId { get; private set; }

    public Guid? PilotUserId { get; private set; }

    public string MissionCode { get; private set; } = null!;

    public MissionType MissionType { get; private set; }

    public MissionStatus Status { get; private set; }

    public ProcessingStatus ProcessingStatus { get; private set; }

    public DateTimeOffset? ScheduledAt { get; private set; }

    public DateTimeOffset? ScheduledEndAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public LineString? FlightRoute { get; private set; }

    public JsonDocument FlightParameters { get; private set; } = null!;

    public int? DetectedPlantCount { get; private set; }

    public string? Notes { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? PublishedMapVersionId { get; private set; }

    public Guid? MappingApprovalId { get; private set; }

    public DateTimeOffset? MapPublishedAt { get; private set; }

    public Guid? HealthReviewHandoffId { get; private set; }

    public long? HealthReviewVersion { get; private set; }

    public MissionHealthReviewState? HealthReviewState { get; private set; }

    public int HealthReviewTotal { get; private set; }

    public int HealthReviewPending { get; private set; }

    public int HealthReviewAwaitingFieldVerification { get; private set; }

    public int HealthReviewResolved { get; private set; }

    public DateTimeOffset? HealthReviewChangedAt { get; private set; }

    public Drone Drone { get; private set; } = null!;

    public ICollection<MissionMedia> Media { get; private set; } = [];

    public ICollection<MissionTelemetryPoint> TelemetryPoints { get; private set; } = [];

    public ICollection<AiProcessingJob> AiProcessingJobs { get; private set; } = [];

    public ICollection<MissionPlantObservation> PlantObservations { get; private set; } = [];

    public static DroneMission Create(
    Guid tenantId,
    Guid farmId,
    Guid zoneId,
    Guid droneId,
    Guid? pilotUserId,
    string missionCode,
    MissionType missionType,
    Guid? sourceMapVersionId,
    JsonDocument flightParameters,
    string? notes,
    Guid createdBy,
    DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(zoneId);
        DomainGuard.NotEmpty(droneId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(createdAt);
        if (!Enum.IsDefined(missionType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missionType),
                missionType,
                "Mission type is invalid.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(missionCode);
        ArgumentNullException.ThrowIfNull(flightParameters);

        if (missionType == MissionType.Mapping &&
            sourceMapVersionId.HasValue)
        {
            throw new ArgumentException(
                "A mapping mission cannot use a source map version.",
                nameof(sourceMapVersionId));
        }

        var normalizedMissionCode =
            missionCode.Trim().ToUpperInvariant();

        if (normalizedMissionCode.Length > 50)
        {
            throw new ArgumentException(
                "Mission code cannot exceed 50 characters.",
                nameof(missionCode));
        }

        if (pilotUserId.HasValue)
        {
            DomainGuard.NotEmpty(pilotUserId.Value);
        }

        if (sourceMapVersionId.HasValue)
        {
            DomainGuard.NotEmpty(sourceMapVersionId.Value);
        }

        if (missionType == MissionType.HealthInspection &&
            sourceMapVersionId is null)
        {
            throw new ArgumentException(
                "A health-inspection mission requires a source map version.",
                nameof(sourceMapVersionId));
        }

        return new DroneMission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FarmId = farmId,
            ZoneId = zoneId,
            DroneId = droneId,
            PilotUserId = pilotUserId,
            MissionCode = normalizedMissionCode,
            MissionType = missionType,
            SourceMapVersionId = sourceMapVersionId,
            Status = MissionStatus.Draft,
            ProcessingStatus = ProcessingStatus.NotUploaded,
            FlightParameters = JsonDocument.Parse(
                flightParameters.RootElement.GetRawText()),
            Notes = string.IsNullOrWhiteSpace(notes)
                ? null
                : notes.Trim(),
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }
    public void Schedule(
    DateTimeOffset scheduledAt,
    DateTimeOffset scheduledEndAt,
    DateTimeOffset changedAt)
    {
        DomainGuard.Utc(scheduledAt);
        DomainGuard.Utc(scheduledEndAt);
        DomainGuard.Utc(changedAt);

        EnsureStatus(MissionStatus.Draft);

        if (scheduledEndAt <= scheduledAt)
        {
            throw new ArgumentException(
                "Scheduled end time must be later than scheduled start time.",
                nameof(scheduledEndAt));
        }

        ScheduledAt = scheduledAt;
        ScheduledEndAt = scheduledEndAt;
        Status = MissionStatus.Scheduled;
        UpdatedAt = changedAt;
    }

    public void StartFlight(
        Guid actorId,
        DateTimeOffset startedAt)
    {
        DomainGuard.NotEmpty(actorId);
        DomainGuard.Utc(startedAt);

        EnsureStatus(MissionStatus.Scheduled);

        Status = MissionStatus.InFlight;
        StartedAt = startedAt;
        PreflightConfirmedBy = actorId;
        PreflightConfirmedAt = startedAt;
        UpdatedAt = startedAt;
    }

    public void CompleteFlight(DateTimeOffset completedAt)
    {
        DomainGuard.Utc(completedAt);
        EnsureStatus(MissionStatus.InFlight);

        if (StartedAt.HasValue &&
            completedAt < StartedAt.Value)
        {
            throw new ArgumentException(
                "Flight completion time cannot be earlier than flight start time.",
                nameof(completedAt));
        }

        Status = MissionStatus.FlightCompleted;
        EndedAt = completedAt;
        UpdatedAt = completedAt;
    }

    public void FailFlight(DateTimeOffset failedAt)
    {
        DomainGuard.Utc(failedAt);
        EnsureStatus(MissionStatus.InFlight);

        if (StartedAt.HasValue &&
            failedAt < StartedAt.Value)
        {
            throw new ArgumentException(
                "Flight failure time cannot be earlier than flight start time.",
                nameof(failedAt));
        }

        Status = MissionStatus.FlightFailed;
        EndedAt = failedAt;
        UpdatedAt = failedAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        DomainGuard.Utc(cancelledAt);

        if (Status is not MissionStatus.Draft and
            not MissionStatus.Scheduled)
        {
            throw new InvalidOperationException(
                $"Mission in status '{Status}' cannot be cancelled.");
        }

        Status = MissionStatus.Cancelled;
        UpdatedAt = cancelledAt;
    }

    public void StartUploading(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);

        if (Status is not MissionStatus.FlightCompleted and
            not MissionStatus.UploadFailed)
        {
            throw new InvalidOperationException(
                $"Mission in status '{Status}' cannot start uploading.");
        }

        Status = MissionStatus.Uploading;
        ProcessingStatus = ProcessingStatus.NotUploaded;
        UpdatedAt = changedAt;
    }

    public void FailUploading(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.Uploading);

        Status = MissionStatus.UploadFailed;
        ProcessingStatus = ProcessingStatus.Failed;
        UpdatedAt = changedAt;
    }

    public void MarkReadyForProcessing(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.Uploading);

        Status = MissionStatus.ReadyForProcessing;
        ProcessingStatus = ProcessingStatus.Uploaded;
        UpdatedAt = changedAt;
    }

    public void StartProcessing(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.ReadyForProcessing);

        Status = MissionStatus.Processing;
        ProcessingStatus = ProcessingStatus.Processing;
        UpdatedAt = changedAt;
    }

    public void FailProcessing(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.Processing);

        Status = MissionStatus.ProcessingFailed;
        ProcessingStatus = ProcessingStatus.Failed;
        UpdatedAt = changedAt;
    }

    public void RetryProcessing(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.ProcessingFailed);

        Status = MissionStatus.ReadyForProcessing;
        ProcessingStatus = ProcessingStatus.Uploaded;
        UpdatedAt = changedAt;
    }

    public void MarkAwaitingReview(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.Processing);

        Status = MissionStatus.AwaitingReview;
        ProcessingStatus = ProcessingStatus.ReviewRequired;
        UpdatedAt = changedAt;
    }
    public bool ApplyPublishedZoneMap(
        Guid approvalId,
        Guid mapVersionId,
        DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(approvalId);
        DomainGuard.NotEmpty(mapVersionId);
        DomainGuard.Utc(publishedAt);

        if (PublishedMapVersionId == mapVersionId &&
            MappingApprovalId == approvalId)
        {
            return false;
        }

        if (PublishedMapVersionId.HasValue || MappingApprovalId.HasValue)
        {
            throw new InvalidOperationException(
                "The mapping mission is already linked to a different published map.");
        }

        if (MissionType != MissionType.Mapping ||
            Status != MissionStatus.AwaitingReview ||
            ProcessingStatus != ProcessingStatus.ReviewRequired)
        {
            throw new InvalidOperationException(
                "Only a mapping mission awaiting review can accept a published map.");
        }

        PublishedMapVersionId = mapVersionId;
        MappingApprovalId = approvalId;
        MapPublishedAt = publishedAt;
        Status = MissionStatus.Completed;
        ProcessingStatus = ProcessingStatus.Completed;
        UpdatedAt = publishedAt;
        return true;
    }
    public bool ApplyHealthReviewState(
    Guid handoffId,
    long reviewVersion,
    MissionHealthReviewState state,
    int total,
    int pending,
    int awaitingFieldVerification,
    int resolved,
    DateTimeOffset changedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            handoffId,
            Guid.Empty);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
    reviewVersion);

        if (total < 0 ||
            pending < 0 ||
            awaitingFieldVerification < 0 ||
            resolved < 0 ||
            pending + awaitingFieldVerification + resolved != total)
        {
            throw new ArgumentException(
                "Health review counters are invalid.");
        }

        if (changedAt == default ||
            changedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "ChangedAt must be a non-default UTC timestamp.",
                nameof(changedAt));
        }

        if (MissionType != MissionType.HealthInspection)
        {
            throw new InvalidOperationException(
                "Only a health-inspection mission can accept health review state.");
        }

        if (HealthReviewHandoffId.HasValue &&
            HealthReviewHandoffId != handoffId)
        {
            throw new InvalidOperationException(
                "The Mission belongs to a different health handoff.");
        }

        if (HealthReviewVersion is long currentVersion)
        {
            if (reviewVersion < currentVersion)
            {
                return false;
            }

            if (reviewVersion == currentVersion)
            {
                var isSameSnapshot =
                    HealthReviewHandoffId == handoffId &&
                    HealthReviewState == state &&
                    HealthReviewTotal == total &&
                    HealthReviewPending == pending &&
                    HealthReviewAwaitingFieldVerification ==
                        awaitingFieldVerification &&
                    HealthReviewResolved == resolved;

                if (isSameSnapshot)
                {
                    return false;
                }

                throw new InvalidOperationException(
                    "The same Health Review version contains conflicting data.");
            }
        }

        if (Status is not MissionStatus.AwaitingReview and
            not MissionStatus.Completed)
        {
            throw new InvalidOperationException(
                "Health review state can only be applied to a mission " +
                "awaiting review or already completed.");
        }


        HealthReviewHandoffId = handoffId;
        HealthReviewVersion = reviewVersion;
        HealthReviewState = state;
        HealthReviewTotal = total;
        HealthReviewPending = pending;
        HealthReviewAwaitingFieldVerification =
            awaitingFieldVerification;
        HealthReviewResolved = resolved;
        HealthReviewChangedAt = changedAt;
        UpdatedAt = changedAt;

        var isResolved =
        state == MissionHealthReviewState.Resolved &&
        pending == 0 &&
        awaitingFieldVerification == 0;

        Status = isResolved
            ? MissionStatus.Completed
            : MissionStatus.AwaitingReview;

        ProcessingStatus = isResolved
            ? ProcessingStatus.Completed
            : ProcessingStatus.ReviewRequired;

        return true;
    }
    private void EnsureStatus(MissionStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new InvalidOperationException(
                $"Mission must be in status '{expectedStatus}', " +
                $"but current status is '{Status}'.");
        }
    }
}
