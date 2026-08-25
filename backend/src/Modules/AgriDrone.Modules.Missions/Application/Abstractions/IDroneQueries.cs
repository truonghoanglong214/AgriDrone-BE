using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

namespace AgriDrone.Modules.Missions.Application.Abstractions;

internal interface IDroneQueries
{
    Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableAsync(
        Guid tenantId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default);
}