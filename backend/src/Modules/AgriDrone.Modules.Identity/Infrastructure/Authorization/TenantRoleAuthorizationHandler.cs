using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class TenantRoleAuthorizationHandler(
    IdentityDbContext dbContext,
    ICurrentUser currentUser)
    : AuthorizationHandler<TenantRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        if (currentUser.UserId is not Guid userId ||
            !AuthorizationRouteValues.TryGetGuid(
                context,
                "tenantId",
                out var tenantId,
                out var cancellationToken))
        {
            return;
        }

        var role = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Status == GeneralStatus.Active)
            .Select(membership => (TenantMemberRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (role.HasValue && requirement.AllowedRoles.Contains(role.Value))
        {
            context.Succeed(requirement);
        }
    }
}
