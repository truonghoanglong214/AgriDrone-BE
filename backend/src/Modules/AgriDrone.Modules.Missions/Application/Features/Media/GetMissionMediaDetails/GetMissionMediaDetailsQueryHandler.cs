using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDetails;

internal sealed class GetMissionMediaDetailsQueryHandler(
    IMissionMediaQueries queries, IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<GetMissionMediaDetailsQuery, Result<MissionMediaResponse>>
{
    public async Task<Result<MissionMediaResponse>> Handle(
        GetMissionMediaDetailsQuery request, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Result.Failure<MissionMediaResponse>(MissionError.CurrentTenantRequired());

        var access = await managerAccessService.ResolveFarmAccessAsync(request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != tenantId || access.FarmId != request.FarmId)
            return Result.Failure<MissionMediaResponse>(AppError.Forbidden(
                "MissionMedia.FarmAccessDenied", "The manager is not assigned to this Farm."));

        var media = await queries.GetDetailsAsync(
            tenantId, request.FarmId, request.MissionId, request.MediaId, cancellationToken);
        return media is null
            ? Result.Failure<MissionMediaResponse>(MediaReadErrors.NotFound())
            : Result.Success(media);
    }
}
