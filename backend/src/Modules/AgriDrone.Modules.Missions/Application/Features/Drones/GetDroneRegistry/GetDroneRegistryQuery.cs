using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;
using MediatR;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetDroneRegistry;

public sealed record GetDroneRegistryQuery()
    : IRequest<Result<IReadOnlyList<DroneRegistryItemResponse>>>;
