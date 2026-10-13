namespace AgriDrone.Modules.Missions.Application.Features.Missions.CompleteMissionPreflight;

public sealed record CompleteMissionPreflightResult(
    MissionResponse Mission,
    bool ReusedOperation);
