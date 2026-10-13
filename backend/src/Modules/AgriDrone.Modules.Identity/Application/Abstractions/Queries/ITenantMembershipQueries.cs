using AgriDrone.Modules.Identity.Application.Features.GetUserTenants;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries;

internal interface ITenantMembershipQueries
{
    Task<PagedResult<UserTenantListItemResponse>> GetUserTenantsAsync(
        Guid userId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);
}
