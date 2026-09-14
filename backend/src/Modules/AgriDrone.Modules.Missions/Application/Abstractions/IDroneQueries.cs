using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDroneDetails;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDrones;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application.Pagination;

namespace AgriDrone.Modules.Missions.Application.Abstractions;

internal interface IDroneQueries
{
    Task<PagedResult<DroneListItemResponse>>
        GetPageAsync(
            Guid tenantId,
            PagedRequest pageRequest,
            DroneStatus? status,
            string? search,
            CancellationToken cancellationToken = default);

    Task<DroneDetailsResponse?> GetDetailsAsync(
        Guid droneId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableDroneResponse>>
        GetAvailableAsync(
            Guid tenantId,
            DateTimeOffset startAt,
            DateTimeOffset endAt,
            CancellationToken cancellationToken = default);

    Task<PagedResult<DroneListItemResponse>>
        GetSystemPageAsync(
            Guid? tenantId,
            PagedRequest pageRequest,
            DroneStatus? status,
            string? search,
            CancellationToken cancellationToken = default);

    Task<DroneDetailsResponse?>
        GetSystemDetailsAsync(
            Guid droneId,
            CancellationToken cancellationToken = default);
}