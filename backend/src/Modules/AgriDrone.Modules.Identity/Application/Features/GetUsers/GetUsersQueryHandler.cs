using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetUsers
{
    internal sealed class GetUsersQueryHandler(IUserQueries userQueries) : IRequestHandler<GetUsersQuery, Result<PagedResult<UserListItemResponse>>>
    {
        public async Task<Result<PagedResult<UserListItemResponse>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
        {
            var pageRequest = new PagedRequest(
                request.PageNumber,
                request.PageSize);

            var users = await userQueries.GetPageAsync(
                pageRequest,
                cancellationToken);

            return Result.Success(users);
        }
    }
}
