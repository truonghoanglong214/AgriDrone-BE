using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;

public sealed record GetAvailableDronesQuery(
    Guid FarmId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt)
    : IRequest<
        Result<IReadOnlyList<AvailableDroneResponse>>>;
