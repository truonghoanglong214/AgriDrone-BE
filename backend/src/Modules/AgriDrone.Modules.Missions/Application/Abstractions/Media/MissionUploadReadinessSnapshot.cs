namespace AgriDrone.Modules.Missions.Application
    .Abstractions.Media;

internal sealed record MissionUploadReadinessSnapshot(
    int RawImageCount,
    int RawVideoCount,
    bool HasTelemetryImport,
    int ImportedPointCount,
    int PersistedPointCount,
    bool HasActiveUploadSessions);