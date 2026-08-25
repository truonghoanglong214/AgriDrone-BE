namespace AgriDrone.IntegrationContracts.AI;

public sealed record AiJobOutputV1(
    string OutputType,
    string StorageUri,
    string? MimeType,
    long? FileSizeBytes,
    string? ChecksumAlgorithm,
    string? Checksum);