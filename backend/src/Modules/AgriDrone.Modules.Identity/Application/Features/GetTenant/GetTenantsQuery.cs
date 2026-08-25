using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenant
{
    public sealed record GetTenantsQuery(
        int PageNumber,
        int PageSize) : IRequest<Result<PagedResult<TenantListItemResponse>>>;
}
