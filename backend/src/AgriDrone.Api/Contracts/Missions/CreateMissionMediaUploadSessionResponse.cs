namespace AgriDrone.Api.Contracts.Missions;

public sealed record CreateMissionMediaUploadSessionResponse(
    Guid UploadSessionId,
    Guid MediaAssetId,
    string UploadUri,
    DateTimeOffset ExpiresAt,
    string Status,
    uint MissionVersion,
    bool ReusedOperation);