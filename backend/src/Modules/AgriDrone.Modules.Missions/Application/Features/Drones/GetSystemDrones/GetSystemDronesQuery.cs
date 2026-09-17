using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetSystemDrones;

public sealed record GetSystemDronesQuery(
    Guid? TenantId,
    int PageNumber,
    int PageSize,
    DroneStatus? Status,
    string? Search)
    : IRequest<
        Result<PagedResult<DroneListItemResponse>>>;