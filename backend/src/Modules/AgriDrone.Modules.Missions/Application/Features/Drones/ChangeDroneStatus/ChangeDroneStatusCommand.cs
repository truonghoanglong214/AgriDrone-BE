using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Drones.ChangeDroneStatus;

public sealed record ChangeDroneStatusCommand(
    Guid DroneId,
    DroneStatus TargetStatus,
    DateTimeOffset? NextMaintenanceAt,
    uint? ExpectedVersion = null,
    string? Reason = null)
    : IRequest<Result<ChangeDroneStatusResponse>>;
