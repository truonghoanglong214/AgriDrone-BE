using AgriDrone.Modules.Farms.Application.Features.GetFarm;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;
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
                .Where(farm =>
                    farm.TenantId == tenantId &&
                    farm.DeletedAt == null)
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

        public Task<PagedResult<ArchivedFarmResponse>> GetArchivedFarmsPageAsync(
            Guid tenantId,
            PagedRequest pagedRequest,
            CancellationToken cancellationToken)
        {
            return context.Farms
                .AsNoTracking()
                .Where(farm =>
                    farm.TenantId == tenantId &&
                    farm.DeletedAt != null)
                .OrderByDescending(farm => farm.DeletedAt)
                .ThenByDescending(farm => farm.Id)
                .Select(farm => new ArchivedFarmResponse(
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
                    farm.CreatedBy,
                    farm.UpdatedAt,
                    farm.DeletedAt!.Value,
                    farm.Version))
                .ToPagedResultAsync(
                    pagedRequest,
                    cancellationToken);
        }

        public Task<ArchivedFarmResponse?> GetArchivedFarmByIdAsync(
            Guid tenantId,
            Guid farmId,
            CancellationToken cancellationToken)
        {
            return context.Farms
                .AsNoTracking()
                .Where(farm =>
                    farm.TenantId == tenantId &&
                    farm.Id == farmId &&
                    farm.DeletedAt != null)
                .Select(farm => new ArchivedFarmResponse(
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
                    farm.CreatedBy,
                    farm.UpdatedAt,
                    farm.DeletedAt!.Value,
                    farm.Version))
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
