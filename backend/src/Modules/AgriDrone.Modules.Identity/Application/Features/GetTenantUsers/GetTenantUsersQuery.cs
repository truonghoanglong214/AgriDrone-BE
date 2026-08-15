using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenantUsers
{
    public sealed record GetTenantUsersQuery(
         int PageNumber,
         int PageSize) : IRequest<Result<PagedResult<TenantUsersListItemResponse>>>;
}
