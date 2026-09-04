using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.SharedInfrastructure.Authorization;

internal sealed class ZoneAccessAuthorizationHandler(
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : AuthorizationHandler<ZoneAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ZoneAccessRequirement requirement)
    {
        if (executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid tenantId ||
            context.Resource is not ZoneAccessTarget target ||
            target.TenantId != tenantId)
        {
            return;
        }

        var decision = await effectiveAccessService.CheckZoneAsync(
            actorId,
            tenantId,
            target.FarmId,
            target.ZoneId,
            requirement.RequiredAccess,
            CancellationToken.None);

        if (decision.IsAllowed)
        {
            context.Succeed(requirement);
        }
    }
}
