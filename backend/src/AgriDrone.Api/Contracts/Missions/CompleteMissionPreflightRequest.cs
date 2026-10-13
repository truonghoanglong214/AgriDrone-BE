using System.Text.Json;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record CompleteMissionPreflightRequest(
    Guid OperationId,
    uint ExpectedVersion,
    Guid ChecklistDefinitionId,
    string ChecklistVersion,
    JsonElement Answers,
    bool SuitableForFlight,
    string? Notes,
    DateTimeOffset? DeviceCompletedAt = null,
    string? UnsuitableConditionNotes = null,
    string? FailsafeNotes = null,
    string? FlightAuthorizationEvidence = null);
