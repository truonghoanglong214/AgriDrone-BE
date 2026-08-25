using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Infrastructure.Queries;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenant
{
    internal sealed class GetTenantQueryHandler(
        ITenantQueries tenantQueries
        ) : IRequestHandler<GetTenantsQuery, Result<PagedResult<TenantListItemResponse>>>
    {
        public async Task<Result<PagedResult<TenantListItemResponse>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
        {
            var pageRequest = new PagedRequest(
                request.PageNumber,
                request.PageSize);

            var tenants = await tenantQueries.GetPageResultAsync(
                pageRequest,
                cancellationToken);

            return Result.Success(tenants);
        }
    }
}
