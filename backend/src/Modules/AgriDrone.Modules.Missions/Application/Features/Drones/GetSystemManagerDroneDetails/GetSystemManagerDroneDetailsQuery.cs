using AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetSystemManagerDroneDetails;

public sealed record GetSystemManagerDroneDetailsQuery(Guid FarmId, Guid DroneId)
    : IRequest<Result<DroneRegistryItemResponse>>;
