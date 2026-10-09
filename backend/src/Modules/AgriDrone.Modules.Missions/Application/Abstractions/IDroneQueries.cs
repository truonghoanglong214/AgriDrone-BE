using AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;

namespace AgriDrone.Modules.Missions.Application.Abstractions;

internal interface IDroneQueries
{
    Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableAsync(
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableDroneResponse>> GetAvailableExcludingMissionAsync(
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid excludedMissionId,
        CancellationToken cancellationToken = default) =>
        GetAvailableAsync(startAt, endAt, cancellationToken);

    Task<IReadOnlyList<DroneRegistryItemResponse>> GetRegistryAsync(
        CancellationToken cancellationToken = default);
}
