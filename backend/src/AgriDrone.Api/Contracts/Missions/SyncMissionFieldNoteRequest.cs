namespace AgriDrone.Api.Contracts.Missions;

public sealed record SyncMissionFieldNoteRequest(
    Guid OperationId, string Text, DateTimeOffset ObservedAt);
