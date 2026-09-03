namespace AgriDrone.IntegrationContracts.AI;

public sealed record AiJobInputV1(
    Guid MediaAssetId,
    string Role,
    string StorageUri,
    string MimeType,
    long FileSizeBytes,
    string ChecksumAlgorithm,
    string Checksum);