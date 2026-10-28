using MediatR;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetAllPreflightChecklistDefinitions;

public sealed record GetAllPreflightChecklistDefinitionsQuery
    : IRequest<Result<IReadOnlyList<PreflightChecklistDefinitionResponse>>>;
