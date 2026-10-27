namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionTelemetrySegmentRequest(
    int SegmentIndex,
    IReadOnlyList<MissionTelemetryPointRequest>? Points);
