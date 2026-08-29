using AgriDrone.Modules.Farms.Application.Abstractions.Features.GetFarm;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Infrastructure.Queries
{
    internal sealed class FarmQueries(
        FarmsDbContext context) : IFarmQueries
    {
        public Task<PagedResult<FarmListItemResponse>> GetFarmsPageAsync(Guid tenantId, PagedRequest pagedRequest, CancellationToken cancellationToken)
        {
            var farms = context.Farms
                .AsNoTracking()
                .OrderByDescending(farm => farm.CreatedAt)
                .ThenByDescending(farm => farm.Id)
                .Where(farm => farm.TenantId == tenantId)
                .Select(
                farm => new FarmListItemResponse(
                    farm.Id,
                    farm.TenantId,
                    farm.Code,
                    farm.Name,
                    farm.Address,
                    farm.Boundary,
                    farm.CenterPoint,
                    farm.AreaHectares,
                    farm.Status,
                    farm.CreatedAt,
                    farm.CreatedBy))
                .ToPagedResultAsync(
                    pagedRequest,
                    cancellationToken);

            return farms;
        }
    }
}
