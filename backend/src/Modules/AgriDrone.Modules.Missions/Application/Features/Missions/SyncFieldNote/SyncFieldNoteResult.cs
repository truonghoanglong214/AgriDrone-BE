namespace AgriDrone.Modules.Missions.Application.Features.Missions.SyncFieldNote;

public sealed record SyncFieldNoteResult(
    Guid NoteId, Guid OperationId, DateTimeOffset ObservedAt,
    DateTimeOffset ReceivedAt, bool ReusedOperation);
