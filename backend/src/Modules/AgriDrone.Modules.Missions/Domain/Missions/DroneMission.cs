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

    public Guid? ZoneId { get; private set; }

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

    public bool ApplyPublishedZoneMap(
        Guid approvalId,
        Guid mapVersionId,
        DateTimeOffset publishedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(approvalId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(mapVersionId, Guid.Empty);

        if (publishedAt == default || publishedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "PublishedAt must be a non-default UTC timestamp.",
                nameof(publishedAt));
        }

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
            Status != MissionStatus.Completed ||
            ProcessingStatus != ProcessingStatus.ReviewRequired)
        {
            throw new InvalidOperationException(
                "Only a completed mapping flight awaiting review can accept a published map.");
        }

        PublishedMapVersionId = mapVersionId;
        MappingApprovalId = approvalId;
        MapPublishedAt = publishedAt;
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

        ProcessingStatus =
            state == MissionHealthReviewState.Resolved &&
            pending == 0 &&
            awaitingFieldVerification == 0
                ? ProcessingStatus.Completed
                : ProcessingStatus.ReviewRequired;

        return true;
    }
}
