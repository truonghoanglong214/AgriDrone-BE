using AgriDrone.Modules.Farms.Application.Features.GetFarm;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Abstractions.Queries
{
    internal interface IFarmQueries
    {
        Task<PagedResult<FarmListItemResponse>> GetFarmsPageAsync(
            Guid tenantId,
            PagedRequest pagedRequest,
            CancellationToken cancellationToken);
    }
}
