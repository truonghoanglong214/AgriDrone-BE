namespace AgriDrone.IntegrationContracts.Media;

public sealed record MediaUploadCompletionV1(
    Guid OperationId,
    Guid UploadSessionId,
    Guid MediaAssetId,
    string StorageUri,
    long FileSizeBytes,
    MediaChecksumV1 Checksum,
    DateTimeOffset CompletedAt);