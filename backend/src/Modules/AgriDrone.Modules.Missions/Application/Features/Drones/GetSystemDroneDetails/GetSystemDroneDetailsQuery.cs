using AgriDrone.Modules.Missions.Application
    .Features.Drones.GetDroneDetails;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetSystemDroneDetails;

public sealed record GetSystemDroneDetailsQuery(
    Guid DroneId)
    : IRequest<Result<DroneDetailsResponse>>;