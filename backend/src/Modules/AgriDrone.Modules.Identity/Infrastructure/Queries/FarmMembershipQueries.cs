using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
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
}
