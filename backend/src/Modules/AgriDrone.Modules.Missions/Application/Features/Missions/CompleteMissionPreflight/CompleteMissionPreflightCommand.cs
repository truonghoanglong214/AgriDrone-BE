using System.Text.Json;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

public sealed record CompleteMissionPreflightCommand(
    Guid FarmId,
    Guid MissionId,
    Guid OperationId,
    uint ExpectedMissionVersion,
    Guid ChecklistDefinitionId,
    int ChecklistVersion,
    JsonDocument Answers,
    bool SuitableForFlight,
    string? Notes,
    DateTimeOffset? DeviceCompletedAt = null)
    : IRequest<Result<CompleteMissionPreflightResult>>;
