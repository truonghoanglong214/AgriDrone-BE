using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDroneDetails;

public sealed record GetDroneDetailsQuery(
    Guid TenantId,
    Guid DroneId)
    : IRequest<Result<DroneDetailsResponse>>;