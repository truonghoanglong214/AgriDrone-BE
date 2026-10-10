namespace AgriDrone.Api.Contracts.Missions;

public sealed record OperateMissionRequest(
    uint ExpectedVersion,
    string? Reason,
    Guid? IncidentOperationId = null,
    string? IncidentType = null,
    string? IncidentOutcome = null,
    string? RecoveryDecision = null,
    string? EvidenceReference = null);
