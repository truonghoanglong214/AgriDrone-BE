namespace AgriDrone.Modules.Missions.Application.Abstractions.Media;

public sealed record ObjectUploadRequest(
    Guid TenantId,
    Guid MediaAssetId,
    string ObjectName,
    string MimeType,
    long FileSizeBytes,
    string ChecksumAlgorithm,
    string Checksum);