using AgriDrone.Modules.Identity.Domain.Tenants;
using AgriDrone.Modules.Identity.Domain.Users;
using AgriDrone.Modules.Identity.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class TenantRoleAuthorizationHandler(
    IdentityDbContext dbContext,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant)
    : AuthorizationHandler<TenantRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        if (currentUser.UserId is not Guid userId ||
            currentTenant.TenantId is not Guid tenantId)
        {
            return;
        }

        var httpContext = context.Resource as HttpContext;
        var routeTenantValue = httpContext?.Request
            .RouteValues["tenantId"]?
            .ToString();

        if (routeTenantValue is not null &&
            (!Guid.TryParse(routeTenantValue, out var routeTenantId) ||
             routeTenantId != tenantId))
        {
            return;
        }

        var cancellationToken = httpContext?.RequestAborted ??
                                CancellationToken.None;

        var role = await dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Status == GeneralStatus.Active &&
                membership.Tenant.Status == GeneralStatus.Active &&
                membership.Tenant.DeletedAt == null &&
                membership.User.Status == UserStatus.Active &&
                membership.User.DeletedAt == null)
            .Select(membership => (TenantMemberRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

        if (role.HasValue && requirement.AllowedRoles.Contains(role.Value))
        {
            context.Succeed(requirement);
        }
    }
}
