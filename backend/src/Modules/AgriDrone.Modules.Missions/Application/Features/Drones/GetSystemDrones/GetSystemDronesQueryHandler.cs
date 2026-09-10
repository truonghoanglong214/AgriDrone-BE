using AgriDrone.Modules.Missions.Application
    .Abstractions;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetSystemDrones;

internal sealed class GetSystemDronesQueryHandler(
    IDroneQueries droneQueries)
    : IRequestHandler<
        GetSystemDronesQuery,
        Result<PagedResult<DroneListItemResponse>>>
{
    public async Task<
        Result<PagedResult<DroneListItemResponse>>> Handle(
            GetSystemDronesQuery request,
            CancellationToken cancellationToken)
    {
        var pageRequest = new PagedRequest(
            request.PageNumber,
            request.PageSize);

        var drones =
            await droneQueries.GetSystemPageAsync(
                tenantId: request.TenantId,
                pageRequest: pageRequest,
                status: request.Status,
                search: request.Search,
                cancellationToken: cancellationToken);

        return Result.Success(drones);
    }
}