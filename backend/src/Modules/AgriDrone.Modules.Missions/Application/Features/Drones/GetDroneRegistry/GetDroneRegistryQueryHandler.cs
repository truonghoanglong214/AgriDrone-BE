using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;

internal sealed class GetDroneRegistryQueryHandler(IDroneQueries droneQueries)
    : IRequestHandler<
        GetDroneRegistryQuery,
        Result<IReadOnlyList<DroneRegistryItemResponse>>>
{
    public async Task<Result<IReadOnlyList<DroneRegistryItemResponse>>> Handle(
        GetDroneRegistryQuery request,
        CancellationToken cancellationToken)
    {
        var drones = await droneQueries.GetRegistryAsync(cancellationToken);
        return Result.Success(drones);
    }
}
