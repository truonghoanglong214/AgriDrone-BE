namespace AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;

public sealed record NormalizedTelemetryLog(
    string SourceFileName,
    string SourceChecksum,
    IReadOnlyList<NormalizedTelemetrySegment> Segments,
    IReadOnlyList<string> Warnings);