namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

public sealed record ImportTelemetryResult(
    Guid TelemetryImportId,
    Guid MissionId,
    int PointCount,
    DateTimeOffset FirstRecordedAt,
    DateTimeOffset LastRecordedAt,
    uint MissionVersion,
    bool ReusedOperation);