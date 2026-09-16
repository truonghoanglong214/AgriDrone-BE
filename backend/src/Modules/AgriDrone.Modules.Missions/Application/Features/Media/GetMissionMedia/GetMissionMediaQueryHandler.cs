using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMedia;

internal sealed class GetMissionMediaQueryHandler(
    IMissionMediaQueries queries, IExecutionContext executionContext)
    : IRequestHandler<GetMissionMediaQuery, Result<PagedResult<MissionMediaResponse>>>
{
    public async Task<Result<PagedResult<MissionMediaResponse>>> Handle(
        GetMissionMediaQuery request, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Result.Failure<PagedResult<MissionMediaResponse>>(MissionError.CurrentTenantRequired());

        if (!await queries.MissionExistsAsync(tenantId, request.FarmId, request.MissionId, cancellationToken))
            return Result.Failure<PagedResult<MissionMediaResponse>>(MissionError.NotFound(request.MissionId));

        var page = await queries.GetPageAsync(
            tenantId, request.FarmId, request.MissionId,
            new PagedRequest(request.PageNumber, request.PageSize),
            request.MediaType, request.MediaRole, cancellationToken);
        return Result.Success(page);
    }
}
