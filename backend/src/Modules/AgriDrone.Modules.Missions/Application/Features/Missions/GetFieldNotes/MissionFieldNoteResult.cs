namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetFieldNotes;

public sealed record MissionFieldNoteResult(
    Guid NoteId, Guid OperationId, Guid CreatedBy, string Text,
    DateTimeOffset ObservedAt, DateTimeOffset ReceivedAt,
    string? IncidentType, string? IncidentOutcome,
    string? RecoveryDecision, string? EvidenceReference);
