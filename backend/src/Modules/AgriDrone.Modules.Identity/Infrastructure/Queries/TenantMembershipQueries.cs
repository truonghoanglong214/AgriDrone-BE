using AgriDrone.Modules.Identity.Application.Abstractions;
using AgriDrone.Modules.Identity.Application.Features.GetTenantUsers;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries;

internal sealed class TenantMembershipQueries(IdentityDbContext dbContext)
    : ITenantMembershipQueries
{
    public Task<PagedResult<TenantUsersListItemResponse>> GetUsersPageAsync(
        Guid tenantId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        return dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId &&
                membership.Status == GeneralStatus.Active &&
                membership.Tenant.Status == GeneralStatus.Active &&
                membership.Tenant.DeletedAt == null &&
                membership.User.DeletedAt == null)
            .OrderByDescending(membership => membership.JoinedAt)
            .ThenByDescending(membership => membership.Id)
            .Select(membership => new TenantUsersListItemResponse(
                membership.User.Id,
                membership.User.Email,
                membership.User.FullName,
                membership.User.Phone,
                membership.User.Status,
                membership.Role,
                membership.JoinedAt))
            .ToPagedResultAsync(pagedRequest, cancellationToken);
    }
}
