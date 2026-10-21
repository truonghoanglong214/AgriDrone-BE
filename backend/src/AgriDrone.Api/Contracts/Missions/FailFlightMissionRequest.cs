namespace AgriDrone.Api.Contracts.Missions;

public sealed record FailFlightMissionRequest(
    uint ExpectedMissionVersion,
    string? Reason,
    Guid? IncidentOperationId = null,
    string? IncidentType = null,
    string? IncidentOutcome = null,
    string? RecoveryDecision = null,
    string? EvidenceReference = null);
