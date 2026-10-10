using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;

public sealed record TransitionMissionCommand(
    Guid FarmId,
    Guid MissionId,
    MissionStatus TargetStatus,
    uint ExpectedVersion,
    string? Reason,
    Guid? IncidentOperationId = null,
    string? IncidentType = null,
    string? IncidentOutcome = null,
    string? RecoveryDecision = null,
    string? EvidenceReference = null)
    : IRequest<Result<MissionResponse>>;
