namespace AgriDrone.Api.Contracts.Missions;

public sealed record ImportMissionTelemetryRequest(
    Guid IdempotencyKey,
    string SourceFileName,
    string SourceChecksum,
    uint ExpectedMissionVersion,
    IReadOnlyList<MissionTelemetryPointRequest>? Points,
    int? SegmentIndex = null);
