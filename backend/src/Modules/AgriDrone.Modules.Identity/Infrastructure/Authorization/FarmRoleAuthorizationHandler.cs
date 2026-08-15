using AgriDrone.Modules.Identity.Domain.FarmMemberships;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class FarmRoleAuthorizationHandler(
    IdentityDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant)
    : AuthorizationHandler<FarmRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FarmRoleRequirement requirement)
    {
        if (currentUser.UserId is not Guid userId ||
            currentTenant.TenantId is not Guid tenantId ||
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
                farmMembership.TenantId == tenantId &&
                farmMembership.UserId == userId &&
                farmMembership.Status == GeneralStatus.Active)
            .Join(
                dbContext.TenantMemberships.AsNoTracking().Where(
                    tenantMembership =>
                        tenantMembership.TenantId == tenantId &&
                        tenantMembership.UserId == userId &&
                        tenantMembership.Status == GeneralStatus.Active &&
                        tenantMembership.Tenant.Status == GeneralStatus.Active &&
                        tenantMembership.Tenant.DeletedAt == null &&
                        tenantMembership.User.Status == UserStatus.Active &&
                        tenantMembership.User.DeletedAt == null),
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
