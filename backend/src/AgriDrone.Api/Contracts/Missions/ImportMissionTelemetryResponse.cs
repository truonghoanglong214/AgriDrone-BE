namespace AgriDrone.Api.Contracts.Missions;

public sealed record ImportMissionTelemetryResponse(
    Guid TelemetryImportId,
    Guid MissionId,
    int PointCount,
    DateTimeOffset FirstRecordedAt,
    DateTimeOffset LastRecordedAt,
    uint MissionVersion,
    bool ReusedOperation);