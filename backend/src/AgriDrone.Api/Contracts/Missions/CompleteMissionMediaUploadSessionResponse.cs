namespace AgriDrone.Api.Contracts.Missions;

public sealed record CompleteMissionMediaUploadSessionResponse(
    Guid UploadSessionId,
    Guid MediaAssetId,
    string Status,
    string MimeType,
    long FileSizeBytes,
    string Sha256Checksum,
    bool ReusedCompletion);