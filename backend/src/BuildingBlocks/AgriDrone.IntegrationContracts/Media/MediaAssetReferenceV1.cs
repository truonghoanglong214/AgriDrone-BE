namespace AgriDrone.IntegrationContracts.Media;

public sealed record MediaAssetReferenceV1(
    Guid MediaAssetId,
    Guid TenantId,
    string StorageUri,
    string MimeType,
    long FileSizeBytes,
    MediaChecksumV1 Checksum);