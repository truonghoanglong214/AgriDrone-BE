using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.GetFarm
{
    internal sealed class GetFarmQueryHandler(
        IFarmQueries farmQueries,
        ICurrentTenant currentTenant) : IRequestHandler<GetFarmQuery, Result<PagedResult<FarmListItemResponse>>>
    {
        public async Task<Result<PagedResult<FarmListItemResponse>>> Handle(GetFarmQuery request, CancellationToken cancellationToken)
        {
            if(currentTenant.TenantId is not Guid tenantId)
            {
                return Result.Failure<PagedResult<FarmListItemResponse>>(AuthenticationError.CurrentTenantRequired());
            }  
            var pageRequest = new PagedRequest(
                request.PageNumber,
                request.PageSize);

            var farms = await farmQueries.GetFarmsPageAsync(tenantId, pageRequest, cancellationToken);

            return Result.Success(farms);
        }
    }
}
