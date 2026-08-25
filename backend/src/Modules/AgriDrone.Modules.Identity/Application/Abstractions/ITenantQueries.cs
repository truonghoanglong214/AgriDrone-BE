using AgriDrone.Modules.Identity.Application.Features.GetTenant;
using AgriDrone.SharedKernel.Application.Pagination;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    public interface ITenantQueries
    {
        Task<PagedResult<TenantListItemResponse>> GetPageResultAsync(PagedRequest pagedRequest, CancellationToken cancellationToken);
    }
}
