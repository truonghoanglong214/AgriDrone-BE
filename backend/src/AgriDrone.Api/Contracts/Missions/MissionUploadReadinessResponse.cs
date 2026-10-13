namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionUploadReadinessResponse(
    Guid MissionId,
    uint MissionVersion,
    string Status,
    bool CanFinalize,
    int RawImageCount,
    int RawVideoCount,
    int TelemetryPointCount,
    bool HasActiveUploadSessions,
    IReadOnlyList<string> Blockers);