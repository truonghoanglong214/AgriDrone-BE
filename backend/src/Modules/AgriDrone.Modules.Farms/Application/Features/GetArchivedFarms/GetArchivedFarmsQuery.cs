using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;

public sealed record GetArchivedFarmsQuery(
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<ArchivedFarmResponse>>>;
