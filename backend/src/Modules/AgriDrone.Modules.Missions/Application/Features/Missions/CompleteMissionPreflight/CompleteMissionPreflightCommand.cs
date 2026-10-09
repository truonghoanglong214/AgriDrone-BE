using System.Text.Json;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

public sealed record CompleteMissionPreflightCommand(
    Guid FarmId,
    Guid MissionId,
    Guid OperationId,
    uint ExpectedVersion,
    Guid ChecklistDefinitionId,
    string ChecklistVersion,
    JsonDocument Answers,
    bool SuitableForFlight,
    string? Notes,
    DateTimeOffset? DeviceCompletedAt = null,
    string? UnsuitableConditionNotes = null,
    string? FailsafeNotes = null)
    : IRequest<Result<CompleteMissionPreflightResult>>;
