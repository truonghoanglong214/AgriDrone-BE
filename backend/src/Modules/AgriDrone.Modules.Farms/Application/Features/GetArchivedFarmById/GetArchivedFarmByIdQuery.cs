using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarmById;

public sealed record GetArchivedFarmByIdQuery(Guid FarmId)
    : IRequest<Result<ArchivedFarmResponse>>;
