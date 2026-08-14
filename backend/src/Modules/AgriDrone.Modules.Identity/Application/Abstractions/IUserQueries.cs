using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    internal interface IUserQueries
    {
        Task<PagedResult<UserListItemResponse>> GetPageAsync(PagedRequest pagedRequest, CancellationToken cancellationToken);
    }
}
