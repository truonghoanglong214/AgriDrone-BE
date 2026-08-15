using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries
{
    internal sealed class UserQueries(IdentityDbContext dbContext) : IUserQueries
    {
        public Task<PagedResult<UserListItemResponse>> GetPageAsync(PagedRequest pagedRequest, CancellationToken cancellationToken)
        {
            return dbContext.Users
                .AsNoTracking()
                .Where(user => user.DeletedAt == null)
                .OrderByDescending(user => user.CreatedAt)
                .ThenByDescending(user => user.Id)
                .Select(user => new UserListItemResponse(
                user.Id,
                user.Email,
                user.FullName,
                user.Phone,
                user.Status,
                user.CreatedAt))
                .ToPagedResultAsync(
                pagedRequest,
                cancellationToken);
        }

    }
}
