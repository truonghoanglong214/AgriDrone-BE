using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record CompleteMissionPreflightRequest(
    Guid OperationId,
    uint ExpectedMissionVersion,
    Guid ChecklistDefinitionId,
    int ChecklistVersion,
    JsonElement Answers,
    bool SuitableForFlight,
    string? Notes,
    DateTimeOffset? DeviceCompletedAt = null);
