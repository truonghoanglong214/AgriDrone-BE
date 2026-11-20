using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.ApproveFarmBaseMapInternally;

internal sealed class ApproveFarmBaseMapInternallyCommandHandler(
    IFarmBaseMapInternalApprovalOrchestrator orchestrator,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext)
    : IRequestHandler<ApproveFarmBaseMapInternallyCommand, Result<FarmBaseMapInternallyApprovedResult>>
{
    public async Task<Result<FarmBaseMapInternallyApprovedResult>> Handle(
        ApproveFarmBaseMapInternallyCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<FarmBaseMapInternallyApprovedResult>(FarmError.NotFound());
        }

        return await orchestrator.ApproveInternalAsync(
            command.FarmId,
            command.DraftId,
            actorId,
            cancellationToken);
    }
}
