using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.GetFarm
{
    public sealed record GetFarmQuery(
        int PageNumber,
        int PageSize) : IRequest<Result<PagedResult<FarmListItemResponse>>>;
}
