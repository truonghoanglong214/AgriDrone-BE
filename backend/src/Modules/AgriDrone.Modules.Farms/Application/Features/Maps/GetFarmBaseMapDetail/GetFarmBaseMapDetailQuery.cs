using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;

public sealed record GetFarmBaseMapDetailQuery(
    Guid FarmId,
    Guid VersionId)
    : IRequest<Result<FarmBaseMapDetailResponse>>;

