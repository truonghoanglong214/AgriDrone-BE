using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CaptureAttempt : AggregateRoot
{
    private readonly List<CaptureAttemptHistory> _history = [];

    private CaptureAttempt() { }

    public Guid OrderId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public int AttemptIndex { get; private set; }
    public CaptureAttemptStatus Status { get; private set; }
    public uint Version { get; private set; }

    public Guid? MissionId { get; private set; }
    public Guid? ParentMissionId { get; private set; }

    // Re-flight / Successor linkage
    public Guid? SupersededAttemptId { get; private set; }
    public Guid? ReflightApprovedBy { get; private set; }
    public string? ReflightApprovalReason { get; private set; }
    public DateTimeOffset? ReflightApprovedAt { get; private set; }

    // Timestamps
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    // Evidence / Proof References
    public string? PreflightEvidenceReference { get; private set; }
    public string? FlightCompletionReceipt { get; private set; }
    public string? FlightFailureEvidence { get; private set; }
    public string? UploadManifestReference { get; private set; }
    public string? UploadManifestFingerprint { get; private set; }
    public string? InputApprovalReference { get; private set; }
    public string? ProcessingJobReference { get; private set; }
    public string? ProcessingAcceptanceReceipt { get; private set; }
    public string? ReflightDecisionEvidence { get; private set; }
    public string? CancellationReason { get; private set; }
    public string? CancellationEvidenceReference { get; private set; }

    // Predecessor History tracking
    public Guid? LastPredecessorHistoryId { get; private set; }

    public IReadOnlyCollection<CaptureAttemptHistory> History => _history.AsReadOnly();

    public bool IsTerminal => IsTerminalStatus(Status);
    public bool IsSuccessor => SupersededAttemptId.HasValue;

    public static bool CanTransition(CaptureAttemptStatus from, CaptureAttemptStatus to) =>
        (from, to) switch
        {
            (CaptureAttemptStatus.Planned, CaptureAttemptStatus.PreflightReady) => true,
            (CaptureAttemptStatus.Planned, CaptureAttemptStatus.Cancelled) => true,
            (CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.Planned) => true,
            (CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.InFlight) => true,
            (CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.Cancelled) => true,
            (CaptureAttemptStatus.InFlight, CaptureAttemptStatus.AwaitingUpload) => true,
            (CaptureAttemptStatus.InFlight, CaptureAttemptStatus.FlightFailed) => true,
            (CaptureAttemptStatus.AwaitingUpload, CaptureAttemptStatus.Uploaded) => true,
            (CaptureAttemptStatus.AwaitingUpload, CaptureAttemptStatus.ReflightRequired) => true,
            (CaptureAttemptStatus.Uploaded, CaptureAttemptStatus.Processing) => true,
            (CaptureAttemptStatus.Uploaded, CaptureAttemptStatus.ReflightRequired) => true,
            (CaptureAttemptStatus.Processing, CaptureAttemptStatus.Accepted) => true,
            (CaptureAttemptStatus.Processing, CaptureAttemptStatus.ReflightRequired) => true,
            _ => false
        };

    public static bool IsTerminalStatus(CaptureAttemptStatus status) =>
        status is CaptureAttemptStatus.Accepted
            or CaptureAttemptStatus.ReflightRequired
            or CaptureAttemptStatus.FlightFailed
            or CaptureAttemptStatus.Cancelled;

    public void EnsureVersion(uint expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw SurveyTransitionGuard.VersionConflict(
                CaptureAttemptDomainErrorCodes.VersionConflict,
                expectedVersion,
                Version);
        }
    }

    public static CaptureAttempt CreateInitial(
        Guid id,
        Guid orderId,
        Guid tenantId,
        Guid farmId,
        Guid? missionId,
        Guid? actorId,
        string source,
        string reason,
        Guid correlationId,
        Guid? operationId,
        string? payloadFingerprint,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(orderId, nameof(orderId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        if (actorId.HasValue && actorId.Value == Guid.Empty)
        {
            throw new ArgumentException("Actor identifier cannot be empty when provided.", nameof(actorId));
        }

        if (missionId.HasValue && missionId.Value == Guid.Empty)
        {
            throw new ArgumentException("Mission identifier cannot be empty.", nameof(missionId));
        }

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateInitial(
            historyId,
            id,
            CaptureAttemptAction.CreateInitial,
            actorId,
            source,
            reason,
            evidenceReference: null,
            correlationId,
            operationId,
            payloadFingerprint,
            createdAt);

        var attempt = new CaptureAttempt
        {
            Id = id,
            OrderId = orderId,
            TenantId = tenantId,
            FarmId = farmId,
            AttemptIndex = 1,
            Status = CaptureAttemptStatus.Planned,
            Version = 1,
            MissionId = missionId,
            ParentMissionId = null,
            SupersededAttemptId = null,
            ReflightApprovedBy = null,
            ReflightApprovalReason = null,
            ReflightApprovedAt = null,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            LastPredecessorHistoryId = historyId
        };

        attempt._history.Add(history);
        return attempt;
    }

    public static CaptureAttempt CreateSuccessor(
        Guid id,
        CaptureAttempt parentAttempt,
        Guid? missionId,
        Guid approvedBy,
        string approvalReason,
        string evidenceReference,
        Guid? actorId,
        string source,
        Guid correlationId,
        Guid? operationId,
        string? payloadFingerprint,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(parentAttempt);
        return CreateSuccessor(
            id,
            parentAttempt.OrderId,
            parentAttempt.TenantId,
            parentAttempt.FarmId,
            parentAttempt,
            missionId,
            approvedBy,
            approvalReason,
            evidenceReference,
            actorId,
            source,
            correlationId,
            operationId,
            payloadFingerprint,
            createdAt);
    }

    public static CaptureAttempt CreateSuccessor(
        Guid id,
        Guid orderId,
        Guid tenantId,
        Guid farmId,
        CaptureAttempt parentAttempt,
        Guid? missionId,
        Guid approvedBy,
        string approvalReason,
        string evidenceReference,
        Guid? actorId,
        string source,
        Guid correlationId,
        Guid? operationId,
        string? payloadFingerprint,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(parentAttempt);
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(orderId, nameof(orderId));
        DomainGuard.NotEmpty(tenantId, nameof(tenantId));
        DomainGuard.NotEmpty(farmId, nameof(farmId));
        DomainGuard.NotEmpty(approvedBy, nameof(approvedBy));
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        if (actorId.HasValue && actorId.Value == Guid.Empty)
        {
            throw new ArgumentException("Actor identifier cannot be empty when provided.", nameof(actorId));
        }

        if (parentAttempt.Id == id)
        {
            throw new ArgumentException("Successor attempt cannot have the same ID as parent attempt.", nameof(id));
        }

        if (parentAttempt.OrderId != orderId || parentAttempt.TenantId != tenantId || parentAttempt.FarmId != farmId)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.CrossOrderParentMismatch,
                "Parent attempt does not belong to the specified Order, Tenant, or Farm.");
        }

        if (parentAttempt.Status != CaptureAttemptStatus.FlightFailed &&
            parentAttempt.Status != CaptureAttemptStatus.ReflightRequired)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.InvalidParentStatus,
                $"Parent attempt must be in FlightFailed or ReflightRequired status to create a quality re-flight successor. Current parent status: '{parentAttempt.Status}'.");
        }

        if (createdAt < parentAttempt.UpdatedAt)
        {
            throw new ArgumentException(
                "Successor attempt creation timestamp cannot be earlier than parent attempt last update timestamp.",
                nameof(createdAt));
        }

        if (parentAttempt.EndedAt.HasValue && createdAt < parentAttempt.EndedAt.Value)
        {
            throw new ArgumentException(
                "Successor attempt creation timestamp cannot be earlier than parent attempt end timestamp.",
                nameof(createdAt));
        }

        if (string.IsNullOrWhiteSpace(approvalReason))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.ReflightApprovalRequired,
                "An approved reason is required to create a quality re-flight successor.");
        }

        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.ReflightApprovalRequired,
                "Approval evidence reference is required to create a quality re-flight successor.");
        }

        if (missionId.HasValue)
        {
            if (missionId.Value == Guid.Empty)
            {
                throw new ArgumentException("Mission identifier cannot be empty.", nameof(missionId));
            }

            if (parentAttempt.MissionId.HasValue && missionId.Value == parentAttempt.MissionId.Value)
            {
                throw new SurveyDomainException(
                    CaptureAttemptDomainErrorCodes.BoundMissionRequired,
                    "Successor attempt cannot reuse the parent attempt's mission ID.");
            }
        }

        var normalizedReason = PolicyValidationGuard.NormalizeRequired(approvalReason, 500, nameof(approvalReason));
        var normalizedEvidence = PolicyValidationGuard.NormalizeRequired(evidenceReference, 500, nameof(evidenceReference));

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateInitial(
            historyId,
            id,
            CaptureAttemptAction.CreateSuccessor,
            actorId,
            source,
            normalizedReason,
            normalizedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            createdAt);

        var attempt = new CaptureAttempt
        {
            Id = id,
            OrderId = orderId,
            TenantId = tenantId,
            FarmId = farmId,
            AttemptIndex = parentAttempt.AttemptIndex + 1,
            Status = CaptureAttemptStatus.Planned,
            Version = 1,
            MissionId = missionId,
            ParentMissionId = parentAttempt.MissionId,
            SupersededAttemptId = parentAttempt.Id,
            ReflightApprovedBy = approvedBy,
            ReflightApprovalReason = normalizedReason,
            ReflightApprovedAt = createdAt,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            LastPredecessorHistoryId = historyId
        };

        attempt._history.Add(history);
        return attempt;
    }

    public void BindMission(Guid missionId)
    {
        DomainGuard.NotEmpty(missionId, nameof(missionId));

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot bind mission to an attempt in terminal status '{Status}'.");
        }

        if (ParentMissionId.HasValue && missionId == ParentMissionId.Value)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.BoundMissionRequired,
                "Successor attempt cannot reuse the parent attempt's mission ID.");
        }

        if (MissionId.HasValue && MissionId.Value != missionId)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.BoundMissionRequired,
                $"Attempt is already bound to mission '{MissionId.Value}', cannot rebind to '{missionId}'.");
        }

        MissionId = missionId;
    }

    public void MarkPreflightReady(
        string preflightEvidenceReference,
        Guid? actorId,
        string source,
        string reason,
        Guid correlationId,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        DateTimeOffset? timestamp = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.MarkPreflightReady;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.PreflightReady;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        if (string.IsNullOrWhiteSpace(preflightEvidenceReference))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.PreflightEvidenceRequired,
                "Preflight evidence reference is required to mark preflight ready.");
        }

        var effectiveTime = timestamp ?? DateTimeOffset.UtcNow;
        DomainGuard.Utc(effectiveTime, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(effectiveTime, UpdatedAt);

        var normalizedEvidence = PolicyValidationGuard.NormalizeRequired(
            preflightEvidenceReference, 500, nameof(preflightEvidenceReference));

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            normalizedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            effectiveTime);

        PreflightEvidenceReference = normalizedEvidence;
        Status = targetStatus;
        Version++;
        UpdatedAt = effectiveTime;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void InvalidatePreflight(
        string reason,
        string? evidenceReference,
        Guid? actorId,
        string source,
        Guid correlationId,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        DateTimeOffset? timestamp = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.InvalidatePreflight;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.Planned;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        var effectiveTime = timestamp ?? DateTimeOffset.UtcNow;
        DomainGuard.Utc(effectiveTime, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(effectiveTime, UpdatedAt);

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            evidenceReference,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            effectiveTime);

        Status = targetStatus;
        Version++;
        UpdatedAt = effectiveTime;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void StartFlight(
        Guid missionId,
        DateTimeOffset startedAt,
        string source,
        string reason,
        string? evidenceReference = null,
        Guid? actorId = null,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.StartFlight;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.InFlight;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        if (missionId == Guid.Empty)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.BoundMissionRequired,
                "A non-empty bound mission ID is required to start flight.");
        }

        if (ParentMissionId.HasValue && missionId == ParentMissionId.Value)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.BoundMissionRequired,
                "Successor attempt cannot reuse the parent attempt's mission ID.");
        }

        if (MissionId.HasValue && MissionId.Value != missionId)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.BoundMissionRequired,
                $"Provided mission ID '{missionId}' does not match bound mission ID '{MissionId.Value}'.");
        }

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(startedAt, nameof(startedAt));
        if (startedAt < CreatedAt)
        {
            throw new ArgumentException(
                "Flight start timestamp cannot be earlier than attempt creation timestamp.",
                nameof(startedAt));
        }

        SurveyTransitionGuard.EnsureTimestamp(startedAt, UpdatedAt);

        if (StartedAt.HasValue)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.InvalidTransition,
                "StartedAt timestamp is already recorded and cannot be rewritten.");
        }

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            evidenceReference,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            startedAt);

        MissionId = missionId;
        StartedAt = startedAt;
        Status = targetStatus;
        Version++;
        UpdatedAt = startedAt;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void RecordFlightCompleted(
        DateTimeOffset endedAt,
        string flightReceiptReference,
        string source,
        string reason,
        Guid? actorId = null,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.RecordFlightCompleted;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.AwaitingUpload;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(endedAt, nameof(endedAt));
        if (StartedAt.HasValue && endedAt < StartedAt.Value)
        {
            throw new ArgumentException(
                "Flight end timestamp cannot be earlier than flight start timestamp.",
                nameof(endedAt));
        }

        SurveyTransitionGuard.EnsureTimestamp(endedAt, UpdatedAt);

        if (string.IsNullOrWhiteSpace(flightReceiptReference))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.FlightCompletionEvidenceRequired,
                "Flight completion receipt reference is required.");
        }

        if (EndedAt.HasValue)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.InvalidTransition,
                "EndedAt timestamp is already recorded and cannot be rewritten.");
        }

        var normalizedReceipt = PolicyValidationGuard.NormalizeRequired(
            flightReceiptReference, 500, nameof(flightReceiptReference));

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            normalizedReceipt,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            endedAt);

        EndedAt = endedAt;
        FlightCompletionReceipt = normalizedReceipt;
        Status = targetStatus;
        Version++;
        UpdatedAt = endedAt;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void RecordFlightFailed(
        DateTimeOffset endedAt,
        string failureEvidence,
        string source,
        string reason,
        Guid? actorId = null,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.RecordFlightFailed;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.FlightFailed;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(endedAt, nameof(endedAt));
        if (StartedAt.HasValue && endedAt < StartedAt.Value)
        {
            throw new ArgumentException(
                "Flight end timestamp cannot be earlier than flight start timestamp.",
                nameof(endedAt));
        }

        SurveyTransitionGuard.EnsureTimestamp(endedAt, UpdatedAt);

        if (string.IsNullOrWhiteSpace(failureEvidence))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.FlightFailureEvidenceRequired,
                "Flight failure evidence reference is required.");
        }

        var normalizedEvidence = PolicyValidationGuard.NormalizeRequired(
            failureEvidence, 500, nameof(failureEvidence));

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            normalizedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            endedAt);

        EndedAt = endedAt;
        FlightFailureEvidence = normalizedEvidence;
        Status = targetStatus;
        Version++;
        UpdatedAt = endedAt;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void MarkUploaded(
        CaptureUploadManifestEvidence manifestEvidence,
        DateTimeOffset timestamp,
        string source,
        string reason,
        Guid? actorId = null,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.MarkUploaded;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.Uploaded;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        ArgumentNullException.ThrowIfNull(manifestEvidence);

        if (manifestEvidence.TenantId != TenantId ||
            manifestEvidence.FarmId != FarmId ||
            manifestEvidence.OrderId != OrderId ||
            manifestEvidence.AttemptId != Id ||
            manifestEvidence.MissionId != MissionId)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch,
                "Upload manifest evidence does not match the attempt identity or mission.");
        }

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(timestamp, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(timestamp, UpdatedAt);

        var combinedEvidence = $"{manifestEvidence.ManifestId}:{manifestEvidence.ManifestFingerprint}";
        if (combinedEvidence.Length > 500)
        {
            combinedEvidence = manifestEvidence.ManifestId;
        }

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            combinedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            timestamp);

        UploadManifestReference = manifestEvidence.ManifestId;
        UploadManifestFingerprint = manifestEvidence.ManifestFingerprint;
        Status = targetStatus;
        Version++;
        UpdatedAt = timestamp;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void StartProcessing(
        CaptureInputApprovalEvidence inputApprovalEvidence,
        DateTimeOffset timestamp,
        string source,
        string reason,
        Guid? actorId = null,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.StartProcessing;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.Processing;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        ArgumentNullException.ThrowIfNull(inputApprovalEvidence);

        if (inputApprovalEvidence.TenantId != TenantId ||
            inputApprovalEvidence.FarmId != FarmId ||
            inputApprovalEvidence.OrderId != OrderId ||
            inputApprovalEvidence.AttemptId != Id ||
            inputApprovalEvidence.MissionId != MissionId)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch,
                "Input approval evidence does not match the attempt identity or mission.");
        }

        if (inputApprovalEvidence.ManifestId != UploadManifestReference)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch,
                $"Input approval manifest '{inputApprovalEvidence.ManifestId}' does not match uploaded manifest '{UploadManifestReference}'.");
        }

        if (inputApprovalEvidence.ManifestFingerprint != UploadManifestFingerprint)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch,
                "Input approval manifest fingerprint does not match uploaded manifest fingerprint.");
        }

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(timestamp, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(timestamp, UpdatedAt);

        var combinedEvidence = $"{inputApprovalEvidence.InputApprovalId}:{inputApprovalEvidence.ProcessingJobId}";
        if (combinedEvidence.Length > 500)
        {
            combinedEvidence = inputApprovalEvidence.InputApprovalId;
        }

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            combinedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            timestamp);

        InputApprovalReference = inputApprovalEvidence.InputApprovalId;
        ProcessingJobReference = inputApprovalEvidence.ProcessingJobId;
        Status = targetStatus;
        Version++;
        UpdatedAt = timestamp;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void AcceptProcessing(
        CaptureProcessingAcceptanceEvidence acceptanceEvidence,
        DateTimeOffset timestamp,
        string source,
        string reason,
        Guid? actorId = null,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.AcceptProcessing;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.Accepted;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        ArgumentNullException.ThrowIfNull(acceptanceEvidence);

        if (acceptanceEvidence.TenantId != TenantId ||
            acceptanceEvidence.FarmId != FarmId ||
            acceptanceEvidence.OrderId != OrderId ||
            acceptanceEvidence.AttemptId != Id ||
            acceptanceEvidence.MissionId != MissionId)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch,
                "Processing acceptance evidence does not match the attempt identity or mission.");
        }

        if (acceptanceEvidence.ProcessingJobId != ProcessingJobReference)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch,
                $"Acceptance evidence job '{acceptanceEvidence.ProcessingJobId}' does not match active processing job '{ProcessingJobReference}'.");
        }

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(timestamp, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(timestamp, UpdatedAt);

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            reason,
            acceptanceEvidence.AcceptanceReceiptId,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            timestamp);

        ProcessingAcceptanceReceipt = acceptanceEvidence.AcceptanceReceiptId;
        Status = targetStatus;
        Version++;
        UpdatedAt = timestamp;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void RequireReflight(
        string qualityEvidence,
        string reason,
        Guid approvedBy,
        DateTimeOffset timestamp,
        string source,
        Guid correlationId = default,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.RequireReflight;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.ReflightRequired;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(timestamp, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(timestamp, UpdatedAt);

        if (approvedBy == Guid.Empty)
        {
            throw new ArgumentException("Approver identifier cannot be empty.", nameof(approvedBy));
        }

        if (string.IsNullOrWhiteSpace(qualityEvidence))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.ReflightApprovalRequired,
                "Quality evidence reference is required to mandate re-flight.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.ReflightApprovalRequired,
                "Approved reason is required to mandate re-flight.");
        }

        var normalizedEvidence = PolicyValidationGuard.NormalizeRequired(
            qualityEvidence, 500, nameof(qualityEvidence));
        var normalizedReason = PolicyValidationGuard.NormalizeRequired(
            reason, 500, nameof(reason));

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            approvedBy,
            source,
            normalizedReason,
            normalizedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            timestamp);

        ReflightDecisionEvidence = normalizedEvidence;
        Status = targetStatus;
        Version++;
        UpdatedAt = timestamp;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    public void Cancel(
        string reason,
        string? evidenceReference,
        Guid? actorId,
        string source,
        Guid correlationId,
        Guid? operationId = null,
        string? payloadFingerprint = null,
        DateTimeOffset? timestamp = null,
        uint? expectedVersion = null)
    {
        const string action = CaptureAttemptAction.Cancel;
        if (CheckIdempotentReplay(operationId, payloadFingerprint, action)) return;

        if (expectedVersion.HasValue) EnsureVersion(expectedVersion.Value);

        if (IsTerminal)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.AttemptTerminal,
                $"Cannot transition terminal attempt '{Id}' from '{Status}'.");
        }

        if (Status != CaptureAttemptStatus.Planned && Status != CaptureAttemptStatus.PreflightReady)
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.CancellationNotAllowed,
                $"Cannot cancel attempt in status '{Status}'. Only Planned or PreflightReady attempts can be cancelled prior to execution.");
        }

        const CaptureAttemptStatus targetStatus = CaptureAttemptStatus.Cancelled;
        SurveyTransitionGuard.EnsureAllowed(
            CanTransition(Status, targetStatus),
            Status,
            targetStatus,
            CaptureAttemptDomainErrorCodes.InvalidTransition);

        var effectiveTime = timestamp ?? DateTimeOffset.UtcNow;
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(effectiveTime, nameof(timestamp));
        SurveyTransitionGuard.EnsureTimestamp(effectiveTime, UpdatedAt);

        var normalizedReason = PolicyValidationGuard.NormalizeRequired(
            reason, 500, nameof(reason));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(
            evidenceReference, 500, nameof(evidenceReference));

        var historyId = Guid.NewGuid();
        var history = CaptureAttemptHistory.CreateTransition(
            historyId,
            Id,
            Version + 1,
            action,
            Status,
            targetStatus,
            actorId,
            source,
            normalizedReason,
            normalizedEvidence,
            correlationId,
            operationId,
            payloadFingerprint,
            LastPredecessorHistoryId!.Value,
            effectiveTime);

        CancellationReason = normalizedReason;
        CancellationEvidenceReference = normalizedEvidence;
        Status = targetStatus;
        Version++;
        UpdatedAt = effectiveTime;
        LastPredecessorHistoryId = historyId;
        _history.Add(history);
    }

    private bool CheckIdempotentReplay(
        Guid? operationId,
        string? payloadFingerprint,
        string action)
    {
        if (operationId.HasValue)
        {
            if (operationId.Value == Guid.Empty)
            {
                throw new ArgumentException("Operation identifier cannot be empty when provided.", nameof(operationId));
            }

            if (string.IsNullOrWhiteSpace(payloadFingerprint))
            {
                throw new ArgumentException("Payload fingerprint is required when operation identifier is provided.", nameof(payloadFingerprint));
            }
        }
        else if (!string.IsNullOrWhiteSpace(payloadFingerprint))
        {
            throw new ArgumentException("Operation identifier is required when payload fingerprint is provided.", nameof(operationId));
        }
        else
        {
            return false;
        }

        var normalizedFingerprint = PolicyValidationGuard.NormalizeRequired(payloadFingerprint, 128, nameof(payloadFingerprint));
        var existingRecord = _history.FirstOrDefault(h => h.OperationId == operationId.Value);

        if (existingRecord != null)
        {
            if (existingRecord.PayloadFingerprint != normalizedFingerprint)
            {
                throw new SurveyDomainException(
                    CaptureAttemptDomainErrorCodes.OperationConflict,
                    $"Operation '{operationId.Value}' was previously recorded with a different payload fingerprint.");
            }

            if (existingRecord.Action != action)
            {
                throw new SurveyDomainException(
                    CaptureAttemptDomainErrorCodes.OperationConflict,
                    $"Operation '{operationId.Value}' was previously recorded for action '{existingRecord.Action}', conflicting with requested action '{action}'.");
            }

            return true;
        }

        return false;
    }
}
