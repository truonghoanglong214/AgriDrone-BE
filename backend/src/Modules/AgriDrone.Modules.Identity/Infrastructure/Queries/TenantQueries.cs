using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Features.GetTenant;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries
{
    internal sealed class TenantQueries(
        IdentityDbContext context) : ITenantQueries
    {
        public Task<PagedResult<TenantListItemResponse>> GetPageResultAsync(PagedRequest pagedRequest, CancellationToken cancellationToken)
        {
            var tenants = context.Tenants
                .AsNoTracking()
                .OrderByDescending(user => user.CreatedAt)
                .ThenByDescending(user => user.Id)
                .Select(tenant => new TenantListItemResponse(
                    tenant.Id,
                    tenant.Code,
                    tenant.Name,
                    tenant.Status,
                    tenant.CreatedAt))
                .ToPagedResultAsync(
                    pagedRequest,
                    cancellationToken);

            return tenants;
        }
    }
}
