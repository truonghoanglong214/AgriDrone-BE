using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDetails;

internal sealed class GetMissionMediaDetailsQueryHandler(
    IMissionMediaQueries queries,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<GetMissionMediaDetailsQuery, Result<MissionMediaResponse>>
{
    public async Task<Result<MissionMediaResponse>> Handle(
        GetMissionMediaDetailsQuery request, CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != request.FarmId)
            return Result.Failure<MissionMediaResponse>(AppError.Forbidden(
                "MissionMedia.FarmAccessDenied", "The manager is not assigned to this Farm."));

        var media = await queries.GetDetailsAsync(
            tenantId, request.FarmId, request.MissionId, request.MediaId, cancellationToken);
        return media is null
            ? Result.Failure<MissionMediaResponse>(MediaReadErrors.NotFound())
            : Result.Success(media);
    }
}
