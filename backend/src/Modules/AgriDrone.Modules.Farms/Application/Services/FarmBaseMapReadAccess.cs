using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;

namespace AgriDrone.Modules.Farms.Application.Services;

internal sealed class FarmBaseMapReadAccess(
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccess,
    ISystemManagerAccessService managerAccess)
{
    public async Task<(bool IsAllowed, bool CanReadDraft)> CheckAsync(
        Guid tenantId, Guid farmId, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return (false, false);
        }

        var manager = await managerAccess.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (manager.IsAllowed && manager.TenantId == tenantId && manager.FarmId == farmId)
        {
            return (true, true);
        }

        if (executionContext.TenantId != tenantId)
        {
            return (false, false);
        }

        var tenantAccess = await effectiveAccess.CheckFarmAsync(
            actorId, tenantId, farmId, FarmAccessLevel.Member, cancellationToken);
        return (tenantAccess.IsAllowed, false);
    }
}
