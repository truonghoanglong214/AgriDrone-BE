namespace AgriDrone.Api.Contracts.Missions;

public sealed record FinalizeMissionUploadRequest(
    uint ExpectedMissionVersion);