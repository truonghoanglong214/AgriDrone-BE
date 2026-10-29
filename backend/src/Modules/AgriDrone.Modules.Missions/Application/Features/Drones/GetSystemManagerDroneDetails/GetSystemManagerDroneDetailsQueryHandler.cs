using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetSystemManagerDroneDetails;

internal sealed class GetSystemManagerDroneDetailsQueryHandler(
    IDroneQueries droneQueries,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<GetSystemManagerDroneDetailsQuery, Result<DroneRegistryItemResponse>>
{
    public async Task<Result<DroneRegistryItemResponse>> Handle(
        GetSystemManagerDroneDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.FarmId != request.FarmId)
        {
            return Result.Failure<DroneRegistryItemResponse>(
                DroneError.FarmAccessDenied(request.FarmId));
        }

        var drone = await droneQueries.GetDetailsAsync(
            request.DroneId, cancellationToken);
        return drone is null
            ? Result.Failure<DroneRegistryItemResponse>(
                DroneError.NotFound(request.DroneId))
            : Result.Success(drone);
    }
}
