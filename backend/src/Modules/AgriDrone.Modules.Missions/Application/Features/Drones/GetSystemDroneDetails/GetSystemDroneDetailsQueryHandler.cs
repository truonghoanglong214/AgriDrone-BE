using AgriDrone.Modules.Missions.Application
    .Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDroneDetails;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetSystemDroneDetails;

internal sealed class GetSystemDroneDetailsQueryHandler(
    IDroneQueries droneQueries)
    : IRequestHandler<
        GetSystemDroneDetailsQuery,
        Result<DroneDetailsResponse>>
{
    public async Task<Result<DroneDetailsResponse>> Handle(
        GetSystemDroneDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var drone =
            await droneQueries.GetSystemDetailsAsync(
                request.DroneId,
                cancellationToken);

        if (drone is null)
        {
            return Result.Failure<DroneDetailsResponse>(
                DroneError.NotFound(request.DroneId));
        }

        return Result.Success(drone);
    }
}