using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDownloadUrl;

internal sealed class GetMissionMediaDownloadUrlQueryHandler(
    IMissionMediaQueries queries,
    IObjectStorage storage,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetMissionMediaDownloadUrlQuery, Result<MissionMediaDownloadResponse>>
{
    public async Task<Result<MissionMediaDownloadResponse>> Handle(
        GetMissionMediaDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Result.Failure<MissionMediaDownloadResponse>(MissionError.CurrentTenantRequired());

        var source = await queries.GetDownloadSourceAsync(
            tenantId, request.FarmId, request.MissionId, request.MediaId, cancellationToken);
        if (source is null || await storage.GetInfoAsync(source, cancellationToken) is null)
            return Result.Failure<MissionMediaDownloadResponse>(MediaReadErrors.NotFound());

        var lifetime = TimeSpan.FromMinutes(5);
        var expiresAt = timeProvider.GetUtcNow().Add(lifetime);
        var url = await storage.CreateDownloadUriAsync(source, lifetime, cancellationToken);
        return Result.Success(new MissionMediaDownloadResponse(
            request.MediaId, url.AbsoluteUri, expiresAt));
    }
}
