using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.ScheduleMission;

public sealed record ScheduleMissionCommand(
    Guid FarmId,
    Guid MissionId,
    DateOnly ScheduledDate,
    uint ExpectedVersion)
    : IRequest<Result<MissionResponse>>;
