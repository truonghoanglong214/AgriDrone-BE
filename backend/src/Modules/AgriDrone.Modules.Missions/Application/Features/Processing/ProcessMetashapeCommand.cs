using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record ProcessMetashapeCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    MetashapeTaskOptions? CustomOptions = null,
    string Engine = "metashape")
    : IRequest<Result<ProcessMetashapeResult>>;

public sealed record ProcessMetashapeResult(
    Guid MissionId,
    string TaskId,
    string Status,
    string Message);
