namespace AgriDrone.Api.Contracts.Missions;

public sealed record ImportMissionTelemetryRequest(
    string SourceFileName,
    string SourceChecksum,
    uint ExpectedMissionVersion,
    IReadOnlyList<MissionTelemetrySegmentRequest>? Segments);
