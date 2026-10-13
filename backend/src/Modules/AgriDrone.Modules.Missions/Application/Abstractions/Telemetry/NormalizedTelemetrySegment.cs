using AgriDrone.Modules.Missions.Application.Features.Telemetry.ImportTelemetry;

namespace AgriDrone.Modules.Missions.Application.Abstractions.Telemetry;

public sealed record NormalizedTelemetrySegment(
    int SegmentIndex,
    int SourcePointCount,
    int RejectedPointCount,
    IReadOnlyList<ImportTelemetryPoint> Points,
    IReadOnlyList<string> Warnings);