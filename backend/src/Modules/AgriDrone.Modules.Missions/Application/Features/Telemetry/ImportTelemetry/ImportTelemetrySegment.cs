namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

public sealed record ImportTelemetrySegment(
    int SegmentIndex,
    IReadOnlyList<ImportTelemetryPoint> Points);
