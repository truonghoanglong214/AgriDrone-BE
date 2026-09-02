using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;

public sealed record TransitionMissionCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    MissionStatus TargetStatus,
    uint ExpectedVersion,
    string? Reason)
    : IRequest<Result<MissionResponse>>;
