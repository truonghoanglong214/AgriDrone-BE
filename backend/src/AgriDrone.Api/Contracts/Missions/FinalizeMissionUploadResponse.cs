namespace AgriDrone.Api.Contracts.Missions;

public sealed record FinalizeMissionUploadResponse(
    Guid MissionId,
    string Status,
    int MediaCount,
    int TelemetryPointCount,
    uint MissionVersion);