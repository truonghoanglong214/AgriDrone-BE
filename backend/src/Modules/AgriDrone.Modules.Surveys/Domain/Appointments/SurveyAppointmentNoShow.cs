using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public enum SurveyNoShowDisposition
{
    FreeReschedule,
    AdminReviewRequired
}

public sealed class SurveyAppointmentNoShow : Entity
{
    private SurveyAppointmentNoShow() { }

    public Guid SurveyOrderId { get; private set; }
    public Guid SurveyAppointmentId { get; private set; }
    public int OccurrenceIndex { get; private set; }
    public SurveyNoShowDisposition Disposition { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string Reason { get; private set; } = null!;
    public string EvidenceReference { get; private set; } = null!;
    public Guid OperationId { get; private set; }
    public string PayloadFingerprint { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SurveyAppointmentNoShow Create(
        Guid id,
        Guid surveyOrderId,
        Guid surveyAppointmentId,
        int occurrenceIndex,
        SurveyNoShowDisposition disposition,
        Guid recordedBy,
        string reason,
        string evidenceReference,
        Guid operationId,
        string payloadFingerprint,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        DomainGuard.NotEmpty(surveyOrderId, nameof(surveyOrderId));
        DomainGuard.NotEmpty(surveyAppointmentId, nameof(surveyAppointmentId));
        DomainGuard.NotEmpty(recordedBy, nameof(recordedBy));
        DomainGuard.NotEmpty(operationId, nameof(operationId));
        DomainGuard.Utc(occurredAt, nameof(occurredAt));
        DomainGuard.Utc(createdAt, nameof(createdAt));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(occurrenceIndex);

        return new SurveyAppointmentNoShow
        {
            Id = id,
            SurveyOrderId = surveyOrderId,
            SurveyAppointmentId = surveyAppointmentId,
            OccurrenceIndex = occurrenceIndex,
            Disposition = disposition,
            RecordedBy = recordedBy,
            Reason = Required(reason, nameof(reason), 500),
            EvidenceReference = Required(evidenceReference, nameof(evidenceReference), 500),
            OperationId = operationId,
            PayloadFingerprint = Required(payloadFingerprint, nameof(payloadFingerprint), 128),
            OccurredAt = occurredAt,
            CreatedAt = createdAt
        };
    }

    private static string Required(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.", name);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.", name);
        return normalized;
    }
}
