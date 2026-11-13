using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class OrderStateHistory : Entity
{
    private OrderStateHistory() { }

    public Guid SurveyOrderId { get; private set; }
    public uint OrderVersion { get; private set; }
    public SurveyOrderStatus? FromStatus { get; private set; }
    public SurveyOrderStatus ToStatus { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Source { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public string? EvidenceReference { get; private set; }
    public Guid CorrelationId { get; private set; }
    public Guid? PredecessorHistoryId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static OrderStateHistory CreateInitial(
        Guid id,
        Guid surveyOrderId,
        uint orderVersion,
        SurveyOrderStatus initialStatus,
        Guid? actorId,
        string source,
        string reason,
        Guid correlationId,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedSource = PolicyValidationGuard.NormalizeRequired(source, 64, nameof(source));
        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reason, 500, nameof(reason));

        return new OrderStateHistory
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            OrderVersion = orderVersion,
            FromStatus = null,
            ToStatus = initialStatus,
            ActorId = actorId,
            Source = normalizedSource,
            Reason = normalizedReason,
            EvidenceReference = null,
            CorrelationId = correlationId,
            PredecessorHistoryId = null,
            CreatedAt = createdAt
        };
    }

    public static OrderStateHistory CreateTransition(
        Guid id,
        Guid surveyOrderId,
        uint orderVersion,
        SurveyOrderStatus fromStatus,
        SurveyOrderStatus toStatus,
        Guid? actorId,
        string source,
        string reason,
        string? evidenceReference,
        Guid correlationId,
        Guid predecessorHistoryId,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(correlationId, nameof(correlationId));
        DomainGuard.NotEmpty(predecessorHistoryId, nameof(predecessorHistoryId));
        DomainGuard.Utc(createdAt, nameof(createdAt));

        var normalizedSource = PolicyValidationGuard.NormalizeRequired(source, 64, nameof(source));
        var normalizedReason = PolicyValidationGuard.NormalizeRequired(reason, 500, nameof(reason));
        var normalizedEvidence = PolicyValidationGuard.NormalizeOptional(evidenceReference, 500, nameof(evidenceReference));

        return new OrderStateHistory
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            OrderVersion = orderVersion,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorId = actorId,
            Source = normalizedSource,
            Reason = normalizedReason,
            EvidenceReference = normalizedEvidence,
            CorrelationId = correlationId,
            PredecessorHistoryId = predecessorHistoryId,
            CreatedAt = createdAt
        };
    }
}
