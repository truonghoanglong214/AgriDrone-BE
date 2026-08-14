using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class FarmRoleAuthorizationHandler(
    IdentityDbContext dbContext,
    ICurrentUser currentUser)
    : AuthorizationHandler<FarmRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FarmRoleRequirement requirement)
    {
        if (currentUser.UserId is not Guid userId ||
            !AuthorizationRouteValues.TryGetGuid(
                context,
                "farmId",
                out var farmId,
                out var cancellationToken))
        {
            return;
        }

        var role = await dbContext.FarmMemberships
            .AsNoTracking()
            .Where(farmMembership =>
                farmMembership.FarmId == farmId &&
                farmMembership.UserId == userId &&
                farmMembership.Status == GeneralStatus.Active)
            .Join(
                dbContext.TenantMemberships.AsNoTracking().Where(
                    tenantMembership =>
                        tenantMembership.UserId == userId &&
                        tenantMembership.Status == GeneralStatus.Active),
                farmMembership => new
                {
                    farmMembership.TenantId,
                    farmMembership.UserId
                },
                tenantMembership => new
                {
                    tenantMembership.TenantId,
                    tenantMembership.UserId
                },
                (farmMembership, _) => (FarmMemberRole?)farmMembership.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (role.HasValue && requirement.AllowedRoles.Contains(role.Value))
        {
            context.Succeed(requirement);
        }
    }
}
