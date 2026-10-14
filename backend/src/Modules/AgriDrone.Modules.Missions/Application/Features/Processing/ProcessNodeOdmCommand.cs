using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record ProcessNodeOdmCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    NodeOdmTaskOptions? CustomOptions = null)
    : IRequest<Result<ProcessNodeOdmResult>>;

public sealed record ProcessNodeOdmResult(
    Guid MissionId,
    string TaskId,
    string Status,
    string Message);
