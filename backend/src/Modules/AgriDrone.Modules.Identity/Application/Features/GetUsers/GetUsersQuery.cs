using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers
{
    public sealed record GetUsersQuery(
        int PageNumber,
        int PageSize) : IRequest<Result<PagedResult<UserListItemResponse>>>;
}
