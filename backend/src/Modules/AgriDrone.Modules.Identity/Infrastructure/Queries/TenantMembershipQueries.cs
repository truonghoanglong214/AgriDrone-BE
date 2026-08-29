using AgriDrone.Modules.Identity.Application.Features.GetTenantUsers;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.GetUserTenants;
using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries;

internal sealed class TenantMembershipQueries(IdentityDbContext dbContext)
    : ITenantMembershipQueries
{
    public Task<PagedResult<UserTenantListItemResponse>> GetUserTenantsAsync(
        Guid userId,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        return dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == userId &&
                membership.Status == GeneralStatus.Active &&
                membership.User.Status == UserStatus.Active &&
                membership.User.DeletedAt == null &&
                membership.Tenant.DeletedAt == null)
            .OrderByDescending(membership => membership.JoinedAt)
            .ThenByDescending(membership => membership.Id)
            .Select(membership => new UserTenantListItemResponse(
                membership.Id,
                membership.Tenant.Id,
                membership.Role,
                membership.Status,
                membership.JoinedAt,
                membership.CreatedAt))
            .ToPagedResultAsync(pagedRequest, cancellationToken);
    }

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
