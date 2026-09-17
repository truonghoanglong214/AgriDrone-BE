using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.GetMissions;

internal sealed class GetMissionsQueryHandler(
    IMissionQueries queries,
    IExecutionContext executionContext)
    : IRequestHandler<GetMissionsQuery, Result<PagedResult<MissionListItemResponse>>>
{
    public async Task<Result<PagedResult<MissionListItemResponse>>> Handle(
        GetMissionsQuery request, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Result.Failure<PagedResult<MissionListItemResponse>>(
                MissionError.CurrentTenantRequired());

        var page = await queries.GetPageAsync(tenantId, request, cancellationToken);
        return Result.Success(page);
    }
}
