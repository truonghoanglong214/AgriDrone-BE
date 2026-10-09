using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.GetAvailableDrones;

public sealed record GetAvailableDronesQuery(
    Guid FarmId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose? Purpose = null)
    : IRequest<
        Result<IReadOnlyList<AvailableDroneResponse>>>;
