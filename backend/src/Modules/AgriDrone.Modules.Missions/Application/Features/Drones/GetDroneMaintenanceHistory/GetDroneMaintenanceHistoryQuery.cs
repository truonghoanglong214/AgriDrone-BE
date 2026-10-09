using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneMaintenanceHistory;

public sealed record GetDroneMaintenanceHistoryQuery(Guid DroneId)
    : IRequest<Result<IReadOnlyList<DroneMaintenanceHistoryResponse>>>;
