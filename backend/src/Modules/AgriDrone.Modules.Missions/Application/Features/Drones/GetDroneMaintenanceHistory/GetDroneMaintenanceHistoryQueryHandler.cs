using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneMaintenanceHistory;

internal sealed class GetDroneMaintenanceHistoryQueryHandler(
    IDroneRepository drones, IDroneMaintenanceRepository maintenance)
    : IRequestHandler<GetDroneMaintenanceHistoryQuery,
        Result<IReadOnlyList<DroneMaintenanceHistoryResponse>>>
{
    public async Task<Result<IReadOnlyList<DroneMaintenanceHistoryResponse>>> Handle(
        GetDroneMaintenanceHistoryQuery request, CancellationToken cancellationToken)
    {
        if (await drones.GetByIdAsync(request.DroneId, cancellationToken) is null)
            return Result.Failure<IReadOnlyList<DroneMaintenanceHistoryResponse>>(
                DroneError.NotFound(request.DroneId));

        var records = await maintenance.ListAsync(request.DroneId, cancellationToken);
        return Result.Success<IReadOnlyList<DroneMaintenanceHistoryResponse>>(
            records.Select(record => new DroneMaintenanceHistoryResponse(
                record.Id, record.DroneId, record.StartedAt, record.StartedBy,
                record.Reason, record.ClosedAt, record.ClosedBy,
                record.ClosingStatus, record.NextMaintenanceAt)).ToArray());
    }
}
