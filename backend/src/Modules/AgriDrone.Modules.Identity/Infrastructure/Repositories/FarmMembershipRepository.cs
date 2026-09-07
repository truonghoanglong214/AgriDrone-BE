using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Repositories;

internal sealed class FarmMembershipRepository(
    IdentityDbContext context) : IFarmMembershipRepository
{
    public void Add(FarmMembership membership) =>
        context.FarmMemberships.Add(membership);

    public Task<FarmMembership?> GetByFarmAndUserAsync(
        Guid tenantId,
        Guid farmId,
        Guid userId,
        CancellationToken cancellationToken) =>
        context.FarmMemberships.SingleOrDefaultAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.FarmId == farmId &&
                membership.UserId == userId,
            cancellationToken);
}
