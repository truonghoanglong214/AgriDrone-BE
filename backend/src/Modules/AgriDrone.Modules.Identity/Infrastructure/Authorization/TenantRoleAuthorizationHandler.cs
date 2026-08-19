using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.Modules.Identity.Infrastructure.Authorization;

internal sealed class TenantRoleAuthorizationHandler(
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : AuthorizationHandler<TenantRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        if (executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid tenantId)
        {
            return;
        }

        var decision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            requirement.RequiredAccess,
            CancellationToken.None);

        if (decision.IsAllowed)
        {
            context.Succeed(requirement);
        }
    }
}
