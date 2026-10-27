using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.PublishFarmBaseMap;

internal sealed class PublishFarmBaseMapCommandHandler(
    IFarmBaseMapPublicationOrchestrator orchestrator,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext)
    : IRequestHandler<PublishFarmBaseMapCommand, Result<FarmBaseMapPublishedResult>>
{
    public async Task<Result<FarmBaseMapPublishedResult>> Handle(
        PublishFarmBaseMapCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<FarmBaseMapPublishedResult>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            command.FarmId,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<FarmBaseMapPublishedResult>(FarmError.NotFound());
        }

        return await orchestrator.PublishAsync(
            command.FarmId,
            command.DraftId,
            actorId,
            cancellationToken);
    }
}
