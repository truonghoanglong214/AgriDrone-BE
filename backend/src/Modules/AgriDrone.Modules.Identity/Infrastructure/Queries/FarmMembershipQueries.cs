using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;
using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Persistence.Pagination;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Queries;

internal sealed class FarmMembershipQueries(
    IdentityDbContext dbContext) : IFarmMembershipQueries
{
    public Task<GetFarmMemberAssignmentResponse?> GetAssignmentAsync(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.FarmMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId &&
                membership.FarmId == farmId &&
                membership.UserId == userId)
            .Select(membership => new GetFarmMemberAssignmentResponse(
                membership.Id,
                membership.TenantId,
                membership.FarmId,
                membership.UserId,
                membership.Role,
                membership.AccessScope,
                membership.ZoneAssignments
                    .Where(assignment => assignment.RevokedAt == null)
                    .OrderBy(assignment => assignment.AssignedAt)
                    .Select(assignment => assignment.ZoneId)
                    .ToArray(),
                membership.Status,
                membership.Version,
                membership.JoinedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PagedResult<FarmMemberListItemResponse>> GetMembersPageAsync(Guid tenantId,
        Guid farmId,
        FarmMemberRole? role,
        GeneralStatus? status,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FarmMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId &&
                membership.FarmId == farmId &&
                membership.TenantMembership.Status == GeneralStatus.Active &&
                membership.User.Status == UserStatus.Active &&
                membership.User.DeletedAt == null);

        if (role.HasValue)
        {
            query = query.Where(membership => membership.Role == role.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(membership => membership.Status == status.Value);
        }

        return query
            .OrderByDescending(membership => membership.JoinedAt)
            .ThenByDescending(membership => membership.Id)
            .Select(membership => new FarmMemberListItemResponse(
                membership.Id,
                membership.FarmId,
                membership.UserId,
                membership.User.Email,
                membership.User.FullName,
                membership.TenantMembership.Role,
                membership.Role,
                membership.AccessScope,
                membership.ZoneAssignments
                    .Where(assignment => assignment.RevokedAt == null)
                    .OrderBy(assignment => assignment.ZoneId)
                    .Select(assignment => assignment.ZoneId)
                    .ToArray(),
                membership.Status,
                membership.Version,
                membership.JoinedAt))
            .ToPagedResultAsync(
                pagedRequest,
                cancellationToken);
    }
}
