namespace AgriDrone.Api.Contracts.Missions;

public sealed record CancelMissionRequest(
    uint ExpectedMissionVersion,
    string? Reason);
