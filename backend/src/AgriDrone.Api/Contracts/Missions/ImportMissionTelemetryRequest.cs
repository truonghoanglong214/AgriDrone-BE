namespace AgriDrone.Api.Contracts.Missions;

public sealed record ImportMissionTelemetryRequest(
    Guid OperationId,
    string SourceFileName,
    string SourceChecksum,
    uint ExpectedMissionVersion,
    IReadOnlyList<MissionTelemetryPointRequest>? Points);