namespace AgriDrone.Api.Contracts.Missions;

public sealed record StartMissionMultipartUploadResponse(
    Guid UploadSessionId, Guid MediaAssetId, long PartSizeBytes,
    int PartCount, DateTimeOffset ExpiresAt,
    IReadOnlyList<UploadedPartResponse> UploadedParts,
    bool ReusedOperation, bool ReusedUpload);
