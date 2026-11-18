using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class CaptureAttemptHistory : Entity
{
    private CaptureAttemptHistory() { }

    public Guid CaptureAttemptId { get; private set; }
    public uint TransitionSequence { get; private set; }
    public uint AttemptVersion => TransitionSequence;
    public string Action { get; private set; } = null!;
    public CaptureAttemptStatus? FromStatus { get; private set; }
    public CaptureAttemptStatus ToStatus { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Source { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid? OperationId { get; private set; }
    public string? PayloadFingerprint { get; private set; }
    public Guid? PredecessorHistoryId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static CaptureAttemptHistory CreateInitial(
        Guid id,
        Guid captureAttemptId,
        string action,
        Guid? actorId,
        string source,
        string reason,
        string? evidenceReference,
        Guid correlationId,
        Guid? operationId,
        string? payloadFingerprint,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(captureAttemptId, nameof(captureAttemptId));
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        if (actorId.HasValue && actorId.Value == Guid.Empty)
        {
            throw new ArgumentException("Actor identifier cannot be empty when provided.", nameof(actorId));
        }

        ValidateIdempotencyEnvelope(operationId, payloadFingerprint);

        var normalizedAction = PolicyValidationGuard.NormalizeRequired(action, 64, nameof(action));
        var normalizedSource = PolicyValidationGuard.NormalizeRequired(source, 64, nameof(source));
        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reason, 500, nameof(reason));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));
        var normalizedFingerprint = payloadFingerprint != null
            ? PolicyValidationGuard.NormalizeRequired(payloadFingerprint, 128, nameof(payloadFingerprint))
            : null;

        return new CaptureAttemptHistory
        {
            Id = id,
            CaptureAttemptId = captureAttemptId,
            TransitionSequence = 1u,
            Action = normalizedAction,
            FromStatus = null,
            ToStatus = CaptureAttemptStatus.Planned,
            ActorId = actorId,
            Source = normalizedSource,
            Reason = normalizedReason,
            EvidenceReference = normalizedEvidence,
            CorrelationId = correlationId,
            OperationId = operationId,
            PayloadFingerprint = normalizedFingerprint,
            PredecessorHistoryId = null,
            CreatedAt = createdAt
        };
    }

    internal static CaptureAttemptHistory CreateTransition(
        Guid id,
        Guid captureAttemptId,
        uint transitionSequence,
        string action,
        CaptureAttemptStatus fromStatus,
        CaptureAttemptStatus toStatus,
        Guid? actorId,
        string source,
        string reason,
        string? evidenceReference,
        Guid correlationId,
        Guid? operationId,
        string? payloadFingerprint,
        Guid predecessorHistoryId,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(captureAttemptId, nameof(captureAttemptId));
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.NotEmpty(predecessorHistoryId, nameof(predecessorHistoryId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        if (transitionSequence < 2u)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transitionSequence),
                transitionSequence,
                "Transition sequence for state transitions must be at least 2.");
        }

        if (predecessorHistoryId == id)
        {
            throw new ArgumentException(
                "A history record cannot reference itself as predecessor.",
                nameof(predecessorHistoryId));
        }

        if (!CaptureAttempt.CanTransition(fromStatus, toStatus))
        {
            throw new SurveyDomainException(
                CaptureAttemptDomainErrorCodes.InvalidTransition,
                $"Transition from '{fromStatus}' to '{toStatus}' is invalid.");
        }

        if (actorId.HasValue && actorId.Value == Guid.Empty)
        {
            throw new ArgumentException("Actor identifier cannot be empty when provided.", nameof(actorId));
        }

        ValidateIdempotencyEnvelope(operationId, payloadFingerprint);

        var normalizedAction = PolicyValidationGuard.NormalizeRequired(action, 64, nameof(action));
        var normalizedSource = PolicyValidationGuard.NormalizeRequired(source, 64, nameof(source));
        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reason, 500, nameof(reason));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));
        var normalizedFingerprint = payloadFingerprint != null
            ? PolicyValidationGuard.NormalizeRequired(payloadFingerprint, 128, nameof(payloadFingerprint))
            : null;

        return new CaptureAttemptHistory
        {
            Id = id,
            CaptureAttemptId = captureAttemptId,
            TransitionSequence = transitionSequence,
            Action = normalizedAction,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorId = actorId,
            Source = normalizedSource,
            Reason = normalizedReason,
            EvidenceReference = normalizedEvidence,
            CorrelationId = correlationId,
            OperationId = operationId,
            PayloadFingerprint = normalizedFingerprint,
            PredecessorHistoryId = predecessorHistoryId,
            CreatedAt = createdAt
        };
    }

    private static void ValidateIdempotencyEnvelope(Guid? operationId, string? payloadFingerprint)
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
    }
}
