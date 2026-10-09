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

    public Guid? SurveyOrderId { get; private set; }

    public MissionPurpose? Purpose { get; private set; }

    public Guid? PreparationOperationId { get; private set; }

    public Guid[] ScopeZoneIds { get; private set; } = [];

    public bool RequiresBaselineCompletion { get; private set; }

    public MissionPurpose? MissionPurpose => Purpose;

    public Guid? SourceMapVersionId { get; private set; }

    public Guid? PreflightConfirmedBy { get; private set; }

    public DateTimeOffset? PreflightConfirmedAt { get; private set; }

    public Guid? PreflightOperationId { get; private set; }

    public string? PreflightChecklistVersion { get; private set; }

    public JsonDocument? PreflightChecklistAnswers { get; private set; }

    public bool? PreflightSuitableForFlight { get; private set; }

    public string? PreflightNotes { get; private set; }

    public uint Version { get; private set; }

    public Guid DroneId { get; private set; }

    public Guid? PilotUserId { get; private set; }

    public string MissionCode { get; private set; } = null!;

    public MissionType? MissionType { get; private set; }

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

    public ICollection<MissionPreflightChecklist> PreflightChecklists { get; private set; } = [];

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

        if (missionType == global::AgriDrone.Modules.Missions.Domain.Missions.MissionType.Mapping &&
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

        if (missionType == global::AgriDrone.Modules.Missions.Domain.Missions.MissionType.HealthInspection &&
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

    public static DroneMission CreateForSurvey(
        SurveyMissionContext surveyContext,
        Guid zoneId,
        Guid droneId,
        Guid? pilotUserId,
        string missionCode,
        Guid? sourceMapVersionId,
        JsonDocument flightParameters,
        string? notes,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(surveyContext);
        DomainGuard.NotEmpty(surveyContext.SurveyOrderId);

        var missionType = surveyContext.Purpose ==
            global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping
            ? global::AgriDrone.Modules.Missions.Domain.Missions.MissionType.Mapping
            : global::AgriDrone.Modules.Missions.Domain.Missions.MissionType.HealthInspection;

        var mission = Create(
            surveyContext.TenantId,
            surveyContext.FarmId,
            zoneId,
            droneId,
            pilotUserId,
            missionCode,
            missionType,
            sourceMapVersionId,
            flightParameters,
            notes,
            createdBy,
            createdAt);

        mission.SurveyOrderId = surveyContext.SurveyOrderId;
        mission.Purpose = surveyContext.Purpose;
        return mission;
    }

    public static DroneMission CreateOrderBound(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        IReadOnlyCollection<Guid> scopeZoneIds,
        Guid droneId,
        Guid pilotUserId,
        string missionCode,
        MissionPurpose purpose,
        Guid preparationOperationId,
        Guid? sourceMapVersionId,
        bool requiresBaselineCompletion,
        JsonDocument flightParameters,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(droneId);
        DomainGuard.NotEmpty(pilotUserId);
        DomainGuard.NotEmpty(preparationOperationId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(createdAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(missionCode);
        ArgumentNullException.ThrowIfNull(scopeZoneIds);
        ArgumentNullException.ThrowIfNull(flightParameters);

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "Mission purpose is invalid.");
        }

        var normalizedScope = scopeZoneIds
            .Where(zoneId => zoneId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedScope.Length != scopeZoneIds.Count)
        {
            throw new ArgumentException(
                "Mission scope cannot contain empty or duplicate Zone identifiers.",
                nameof(scopeZoneIds));
        }

        if (purpose == global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping &&
            sourceMapVersionId.HasValue)
        {
            throw new ArgumentException(
                "A baseline-mapping mission cannot use a source map version.",
                nameof(sourceMapVersionId));
        }

        if (requiresBaselineCompletion &&
            purpose == global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping)
        {
            throw new ArgumentException(
                "The baseline mission cannot depend on its own completion.",
                nameof(requiresBaselineCompletion));
        }

        if (!requiresBaselineCompletion &&
            purpose != global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping &&
            sourceMapVersionId is null)
        {
            throw new ArgumentException(
                "A service mission requires either a current base map or a baseline prerequisite.",
                nameof(sourceMapVersionId));
        }

        var normalizedCode = missionCode.Trim().ToUpperInvariant();
        if (normalizedCode.Length > 50)
        {
            throw new ArgumentException(
                "Mission code cannot exceed 50 characters.",
                nameof(missionCode));
        }

        if (sourceMapVersionId.HasValue)
        {
            DomainGuard.NotEmpty(sourceMapVersionId.Value);
        }

        return new DroneMission
        {
            Id = Guid.NewGuid(),
            SurveyOrderId = surveyOrderId,
            TenantId = tenantId,
            FarmId = farmId,
            ZoneId = normalizedScope.FirstOrDefault() is var primaryZone &&
                     primaryZone != Guid.Empty
                ? primaryZone
                : null,
            ScopeZoneIds = normalizedScope,
            DroneId = droneId,
            PilotUserId = pilotUserId,
            MissionCode = normalizedCode,
            Purpose = purpose,
            MissionType = null,
            PreparationOperationId = preparationOperationId,
            SourceMapVersionId = sourceMapVersionId,
            RequiresBaselineCompletion = requiresBaselineCompletion,
            Status = MissionStatus.Draft,
            ProcessingStatus = ProcessingStatus.NotUploaded,
            FlightParameters = JsonDocument.Parse(
                flightParameters.RootElement.GetRawText()),
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

        if (SurveyOrderId.HasValue &&
            (PreflightConfirmedBy is null ||
             PreflightConfirmedAt is null ||
             PreflightSuitableForFlight != true))
        {
            throw new InvalidOperationException(
                "An order-bound mission requires a suitable completed pre-flight checklist.");
        }

        Status = MissionStatus.InFlight;
        StartedAt = startedAt;
        if (!SurveyOrderId.HasValue)
        {
            PreflightConfirmedBy = actorId;
            PreflightConfirmedAt = startedAt;
        }
        UpdatedAt = startedAt;
    }

    public void CompletePreflight(
        Guid operationId,
        string checklistVersion,
        JsonDocument answers,
        bool suitableForFlight,
        string? notes,
        Guid actorId,
        DateTimeOffset completedAt)
    {
        DomainGuard.NotEmpty(operationId);
        DomainGuard.NotEmpty(actorId);
        DomainGuard.Utc(completedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(checklistVersion);
        ArgumentNullException.ThrowIfNull(answers);
        EnsureStatus(MissionStatus.Scheduled);

        if (PreflightOperationId.HasValue)
        {
            throw new InvalidOperationException(
                "The completed pre-flight checklist snapshot is immutable.");
        }

        var normalizedVersion = checklistVersion.Trim();
        if (normalizedVersion.Length > 100)
        {
            throw new ArgumentException(
                "Checklist version cannot exceed 100 characters.",
                nameof(checklistVersion));
        }

        if (answers.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Checklist answers must be a JSON object.",
                nameof(answers));
        }

        PreflightOperationId = operationId;
        PreflightChecklistVersion = normalizedVersion;
        PreflightChecklistAnswers = JsonDocument.Parse(
            answers.RootElement.GetRawText());
        PreflightSuitableForFlight = suitableForFlight;
        PreflightNotes = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim();
        PreflightConfirmedBy = actorId;
        PreflightConfirmedAt = completedAt;
        UpdatedAt = completedAt;
    }

    public void ReplaceStalePreflight(DateTimeOffset changedAt)
    {
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.Scheduled);
        if (!PreflightOperationId.HasValue)
            throw new InvalidOperationException("No completed pre-flight checklist exists.");

        PreflightConfirmedBy = null;
        PreflightConfirmedAt = null;
        PreflightOperationId = null;
        PreflightChecklistVersion = null;
        PreflightChecklistAnswers = null;
        PreflightSuitableForFlight = null;
        PreflightNotes = null;
        UpdatedAt = changedAt;
    }

    public bool Reschedule(DateTimeOffset scheduledAt,
        DateTimeOffset scheduledEndAt, DateTimeOffset changedAt)
    {
        DomainGuard.Utc(scheduledAt);
        DomainGuard.Utc(scheduledEndAt);
        DomainGuard.Utc(changedAt);
        EnsureStatus(MissionStatus.Scheduled);
        if (SurveyOrderId is null)
            throw new InvalidOperationException("Only an order-bound mission can be rescheduled here.");
        if (scheduledEndAt <= scheduledAt)
            throw new ArgumentException("Scheduled end time must be after its start time.");
        if (ScheduledAt == scheduledAt && ScheduledEndAt == scheduledEndAt)
            return false;

        if (PreflightOperationId.HasValue)
            ReplaceStalePreflight(changedAt);
        ScheduledAt = scheduledAt;
        ScheduledEndAt = scheduledEndAt;
        UpdatedAt = changedAt;
        return true;
    }

    public bool MatchesPreflightOperation(
        Guid operationId,
        string checklistVersion,
        JsonDocument answers,
        bool suitableForFlight,
        string? notes)
    {
        ArgumentNullException.ThrowIfNull(answers);
        return PreflightOperationId == operationId &&
               string.Equals(
                   PreflightChecklistVersion,
                   checklistVersion.Trim(),
                   StringComparison.Ordinal) &&
               PreflightSuitableForFlight == suitableForFlight &&
               string.Equals(
                   PreflightNotes,
                   string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                   StringComparison.Ordinal) &&
               string.Equals(
                   PreflightChecklistAnswers?.RootElement.GetRawText(),
                   answers.RootElement.GetRawText(),
                   StringComparison.Ordinal);
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

        if ((MissionType != global::AgriDrone.Modules.Missions.Domain.Missions.MissionType.Mapping &&
             Purpose != global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping) ||
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

    public bool ApplyPublishedFarmBaseMap(
        Guid surveyOrderId,
        Guid publicationId,
        Guid farmBaseMapVersionId,
        DateTimeOffset publishedAt)
    {
        DomainGuard.NotEmpty(surveyOrderId);
        DomainGuard.NotEmpty(publicationId);
        DomainGuard.NotEmpty(farmBaseMapVersionId);
        DomainGuard.Utc(publishedAt);

        if (SurveyOrderId != surveyOrderId)
        {
            throw new InvalidOperationException(
                "The published base map belongs to a different Survey Order.");
        }

        if (Purpose == global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping)
        {
            if (PublishedMapVersionId == farmBaseMapVersionId &&
                MappingApprovalId == publicationId &&
                Status == MissionStatus.Completed)
            {
                return false;
            }

            if (Purpose != global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.BaselineMapping ||
                Status != MissionStatus.AwaitingReview ||
                ProcessingStatus != ProcessingStatus.ReviewRequired ||
                PublishedMapVersionId.HasValue ||
                MappingApprovalId.HasValue)
            {
                throw new InvalidOperationException(
                    "Only a baseline mission awaiting review can accept a published farm base map.");
            }

            PublishedMapVersionId = farmBaseMapVersionId;
            MappingApprovalId = publicationId;
            MapPublishedAt = publishedAt;
            Status = MissionStatus.Completed;
            ProcessingStatus = ProcessingStatus.Completed;
            UpdatedAt = publishedAt;
            return true;
        }

        if (Purpose is not (
                global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.PlantHealth or
                global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.HarvestReadiness))
        {
            throw new InvalidOperationException(
                "A published farm base map can only be applied to an order mission.");
        }

        if (!RequiresBaselineCompletion && SourceMapVersionId == farmBaseMapVersionId)
        {
            return false;
        }

        if (!RequiresBaselineCompletion ||
            Status is not MissionStatus.Draft and not MissionStatus.Scheduled ||
            SourceMapVersionId.HasValue)
        {
            throw new InvalidOperationException(
                "The service mission is not waiting for this baseline publication.");
        }

        SourceMapVersionId = farmBaseMapVersionId;
        RequiresBaselineCompletion = false;
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

        if (MissionType != global::AgriDrone.Modules.Missions.Domain.Missions.MissionType.HealthInspection &&
            Purpose != global::AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose.PlantHealth)
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

    public void SetActualFlightRoute(
    LineString flightRoute,
    DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(flightRoute);
        DomainGuard.Utc(changedAt);

        EnsureStatus(MissionStatus.Uploading);

        if (flightRoute.IsEmpty ||
            flightRoute.NumPoints < 2)
        {
            throw new ArgumentException(
                "Actual flight route must contain at least two points.",
                nameof(flightRoute));
        }

        if (flightRoute.SRID != 4326)
        {
            throw new ArgumentException(
                "Actual flight route must use SRID 4326.",
                nameof(flightRoute));
        }

        foreach (var coordinate in flightRoute.Coordinates)
        {
            if (!double.IsFinite(coordinate.X) ||
                coordinate.X is < -180 or > 180)
            {
                throw new ArgumentException(
                    "Flight route contains an invalid longitude.",
                    nameof(flightRoute));
            }

            if (!double.IsFinite(coordinate.Y) ||
                coordinate.Y is < -90 or > 90)
            {
                throw new ArgumentException(
                    "Flight route contains an invalid latitude.",
                    nameof(flightRoute));
            }
        }

        FlightRoute = (LineString)flightRoute.Copy();
        FlightRoute.SRID = 4326;
        UpdatedAt = changedAt;
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
