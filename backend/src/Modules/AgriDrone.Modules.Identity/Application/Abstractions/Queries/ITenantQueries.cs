using AgriDrone.Modules.Identity.Application.Features.GetTenant;
using AgriDrone.SharedKernel.Application.Pagination;
namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries
{
    public interface ITenantQueries
    {
        Task<PagedResult<TenantListItemResponse>> GetPageResultAsync(PagedRequest pagedRequest, CancellationToken cancellationToken);
    }
}
