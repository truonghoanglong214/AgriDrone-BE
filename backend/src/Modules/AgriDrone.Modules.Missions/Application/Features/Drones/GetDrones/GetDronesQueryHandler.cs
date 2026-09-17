using AgriDrone.Modules.Missions
    .Application.Abstractions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;

internal sealed class GetDronesQueryHandler(
    IDroneQueries droneQueries)
    : IRequestHandler<
        GetDronesQuery,
        Result<PagedResult<DroneListItemResponse>>>
{
    public async Task<
        Result<PagedResult<DroneListItemResponse>>> Handle(
            GetDronesQuery request,
            CancellationToken cancellationToken)
    {
        var pageRequest = new PagedRequest(
            request.PageNumber,
            request.PageSize);

        var drones = await droneQueries.GetPageAsync(
            tenantId: request.TenantId,
            pageRequest: pageRequest,
            status: request.Status,
            search: request.Search,
            cancellationToken: cancellationToken);

        return Result.Success(drones);
    }
}