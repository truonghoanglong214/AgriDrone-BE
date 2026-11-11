namespace AgriDrone.Api.Contracts.Missions;

public sealed record ResetMissionUploadResponse(
    Guid MissionId,
    string Status,
    int DeletedMediaCount,
    bool TelemetryReset,
    uint MissionVersion);

