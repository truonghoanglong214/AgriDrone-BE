namespace AgriDrone.Api.Contracts.Missions;

public sealed record ResetMissionUploadRequest(
    uint? ExpectedMissionVersion = null,
    bool ResetTelemetry = false);

