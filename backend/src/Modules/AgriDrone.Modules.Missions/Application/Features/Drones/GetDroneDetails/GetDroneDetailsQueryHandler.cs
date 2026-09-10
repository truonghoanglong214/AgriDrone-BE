using AgriDrone.Modules.Missions
    .Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDroneDetails;

internal sealed class GetDroneDetailsQueryHandler(
    IDroneQueries droneQueries)
    : IRequestHandler<
        GetDroneDetailsQuery,
        Result<DroneDetailsResponse>>
{
    public async Task<Result<DroneDetailsResponse>>
        Handle(
            GetDroneDetailsQuery request,
            CancellationToken cancellationToken)
    {
        var drone = await droneQueries.GetDetailsAsync(
            droneId: request.DroneId,
            tenantId: request.TenantId,
            cancellationToken: cancellationToken);

        if (drone is null)
        {
            return Result.Failure<DroneDetailsResponse>(
                DroneError.NotFound(request.DroneId));
        }

        return Result.Success(drone);
    }
}