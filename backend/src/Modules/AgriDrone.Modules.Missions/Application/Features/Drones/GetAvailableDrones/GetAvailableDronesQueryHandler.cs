using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;

internal sealed class GetAvailableDronesQueryHandler(
    IDroneQueries droneQueries,
    ISystemManagerAccessService managerAccessService)
    : IRequestHandler<
        GetAvailableDronesQuery,
        Result<IReadOnlyList<AvailableDroneResponse>>>
{
    public async Task<
        Result<IReadOnlyList<AvailableDroneResponse>>> Handle(
        GetAvailableDronesQuery request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId,
            cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<
                IReadOnlyList<AvailableDroneResponse>>(
                DroneError.FarmAccessDenied(request.FarmId));
        }

        var drones = await droneQueries.GetAvailableAsync(
            request.StartAt,
            request.EndAt,
            cancellationToken);

        return Result.Success(drones);
    }
}
