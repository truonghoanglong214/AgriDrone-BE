using System.Text.Json;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ManagePreflightChecklistDefinition;

public sealed record ManagePreflightChecklistDefinitionCommand(JsonDocument Items)
    : IRequest<Result<ManagePreflightChecklistDefinitionResult>>;
