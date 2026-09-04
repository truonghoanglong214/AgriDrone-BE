using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using Microsoft.AspNetCore.Authorization;

namespace AgriDrone.SharedInfrastructure.Authorization;

internal sealed class FarmAccessAuthorizationHandler(
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : AuthorizationHandler<FarmAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FarmAccessRequirement requirement)
    {
        if (executionContext.ActorId is not Guid actorId ||
            executionContext.TenantId is not Guid tenantId ||
            context.Resource is not FarmAccessTarget target ||
            target.TenantId != tenantId)
        {
            return;
        }

        var decision = await effectiveAccessService.CheckFarmAsync(
            actorId,
            tenantId,
            target.FarmId,
            requirement.RequiredAccess,
            CancellationToken.None);

        if (decision.IsAllowed)
        {
            context.Succeed(requirement);
        }
    }
}
