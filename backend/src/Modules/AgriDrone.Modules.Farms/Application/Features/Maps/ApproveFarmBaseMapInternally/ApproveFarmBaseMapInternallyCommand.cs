using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ApproveFarmBaseMapInternally;

public sealed record ApproveFarmBaseMapInternallyCommand(
    Guid FarmId,
    Guid DraftId) : IRequest<Result<FarmBaseMapInternallyApprovedResult>>;
