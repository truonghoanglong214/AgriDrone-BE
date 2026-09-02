using AgriDrone.Modules.Missions.Domain.Missions;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record TransitionMissionRequest(
    MissionStatus TargetStatus,
    uint ExpectedVersion,
    string? Reason);