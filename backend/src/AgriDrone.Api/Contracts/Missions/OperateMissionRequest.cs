namespace AgriDrone.Api.Contracts.Missions;

public sealed record OperateMissionRequest(
    uint ExpectedVersion,
    string? Reason);
