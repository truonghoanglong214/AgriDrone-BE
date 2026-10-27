using System.Text.Json;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;

public sealed record ManagePreflightChecklistDefinitionCommand(
    JsonDocument? Items,
    JsonDocument? ItemsToAdd = null,
    IReadOnlyList<string>? KeysToRemove = null,
    bool CreateOnly = false)
    : IRequest<Result<ManagePreflightChecklistDefinitionResult>>;
