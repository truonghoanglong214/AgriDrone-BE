namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public sealed record StoredObjectInfo(
    string StorageUri,
    string Provider,
    string StorageKey,
    string MimeType,
    long FileSizeBytes,
    string? ChecksumAlgorithm,
    string? Checksum);