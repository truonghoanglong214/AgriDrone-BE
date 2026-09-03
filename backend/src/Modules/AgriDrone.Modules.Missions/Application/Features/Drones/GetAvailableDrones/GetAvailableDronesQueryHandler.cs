using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;

internal sealed class GetAvailableDronesQueryHandler(
    IDroneQueries droneQueries,
    ICurrentTenant currentTenant)
    : IRequestHandler<
        GetAvailableDronesQuery,
        Result<IReadOnlyList<AvailableDroneResponse>>>
{
    public async Task<
        Result<IReadOnlyList<AvailableDroneResponse>>> Handle(
        GetAvailableDronesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is not Guid tenantId)
        {
            return Result.Failure<
                IReadOnlyList<AvailableDroneResponse>>(
                DroneError.CurrentTenantRequired());
        }

        var drones = await droneQueries.GetAvailableAsync(
            tenantId,
            request.StartAt,
            request.EndAt,
            cancellationToken);

        return Result.Success(drones);
    }
}