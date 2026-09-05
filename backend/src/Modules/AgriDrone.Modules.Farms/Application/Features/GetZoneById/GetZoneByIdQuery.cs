using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetZoneById;

public sealed record GetZoneByIdQuery(Guid FarmId, Guid ZoneId)
    : IRequest<Result<GetZoneByIdResponse>>;
