using AgriDrone.Modules.Identity.Application.Features.GetTenantUsers;
using AgriDrone.Modules.Identity.Application.Features.GetUserTenants;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Identity.Application.Abstractions;

internal interface ITenantMembershipQueries
{
    Task<PagedResult<TenantUsersListItemResponse>> GetUsersPageAsync(
        Guid tenantId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);

    Task<PagedResult<UserTenantListItemResponse>> GetUserTenantsAsync(
        Guid userId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken);
}
