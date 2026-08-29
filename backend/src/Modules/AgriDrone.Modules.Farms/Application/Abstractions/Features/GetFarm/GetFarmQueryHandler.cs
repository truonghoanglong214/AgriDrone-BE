using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Features.GetFarm
{
    internal sealed class GetFarmQueryHandler(
        IFarmQueries farmQueries) : IRequestHandler<GetFarmQuery, Result<PagedResult<FarmListItemResponse>>>
    {
        public async Task<Result<PagedResult<FarmListItemResponse>>> Handle(GetFarmQuery request, CancellationToken cancellationToken)
        {
            var pageRequest = new PagedRequest(
                request.PageNumber,
                request.PageSize);

            var farms = await farmQueries.GetFarmsPageAsync(request.TenantId, pageRequest, cancellationToken);

            return Result.Success(farms);
        }
    }
}
