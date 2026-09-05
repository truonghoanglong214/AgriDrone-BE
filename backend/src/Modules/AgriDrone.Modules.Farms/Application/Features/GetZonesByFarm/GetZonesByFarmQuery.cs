using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;

public sealed record GetZonesByFarmQuery(Guid FarmId)
    : IRequest<Result<IReadOnlyList<ZoneListItemResponse>>>;
