using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Missions.Domain.Missions;

public sealed class MissionFieldNote : Entity
{
    private MissionFieldNote() { }

    public Guid TenantId { get; private set; }
    public Guid FarmId { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid OperationId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public string Text { get; private set; } = null!;
    public DateTimeOffset ObservedAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public string? IncidentType { get; private set; }
    public string? IncidentOutcome { get; private set; }
    public string? RecoveryDecision { get; private set; }
    public string? EvidenceReference { get; private set; }

    public static MissionFieldNote Create(
        Guid tenantId, Guid farmId, Guid missionId, Guid operationId,
        Guid createdBy, string text, DateTimeOffset observedAt,
        DateTimeOffset receivedAt,
        string? incidentType = null,
        string? incidentOutcome = null,
        string? recoveryDecision = null,
        string? evidenceReference = null)
    {
        DomainGuard.NotEmpty(tenantId);
        DomainGuard.NotEmpty(farmId);
        DomainGuard.NotEmpty(missionId);
        DomainGuard.NotEmpty(operationId);
        DomainGuard.NotEmpty(createdBy);
        DomainGuard.Utc(observedAt);
        DomainGuard.Utc(receivedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Trim();
        if (normalized.Length > 2000 || normalized.Any(char.IsControl))
            throw new ArgumentException("Field note must be at most 2000 printable characters.", nameof(text));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            observedAt, receivedAt.AddMinutes(5));
        if (incidentType is null)
        {
            if (incidentOutcome is not null || recoveryDecision is not null ||
                evidenceReference is not null)
                throw new ArgumentException("Incident details require an incident type.");
        }
        else if (incidentType is not ("SIGNAL_LOSS" or "LOW_BATTERY" or
                     "INTERRUPTION" or "FLIGHT_FAILURE") ||
                 string.IsNullOrWhiteSpace(incidentOutcome) ||
                 incidentOutcome.Length > 1000 ||
                 recoveryDecision is not ("CONTINUE" or "ABORT" or
                     "RESCHEDULE_REQUIRED") ||
                 string.IsNullOrWhiteSpace(evidenceReference) ||
                 evidenceReference.Length > 500)
            throw new ArgumentException("Incident type, outcome, recovery decision and evidence reference are required.");

        return new MissionFieldNote
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FarmId = farmId,
            MissionId = missionId, OperationId = operationId,
            CreatedBy = createdBy, Text = normalized,
            ObservedAt = observedAt, ReceivedAt = receivedAt,
            IncidentType = incidentType,
            IncidentOutcome = incidentOutcome?.Trim(),
            RecoveryDecision = recoveryDecision,
            EvidenceReference = evidenceReference?.Trim()
        };
    }

    public bool Matches(Guid createdBy, string text, DateTimeOffset observedAt,
        string? incidentType = null, string? incidentOutcome = null,
        string? recoveryDecision = null, string? evidenceReference = null) =>
        CreatedBy == createdBy &&
        string.Equals(Text, text?.Trim(), StringComparison.Ordinal) &&
        ObservedAt == observedAt &&
        IncidentType == incidentType &&
        IncidentOutcome == incidentOutcome?.Trim() &&
        RecoveryDecision == recoveryDecision &&
        EvidenceReference == evidenceReference?.Trim();
}
