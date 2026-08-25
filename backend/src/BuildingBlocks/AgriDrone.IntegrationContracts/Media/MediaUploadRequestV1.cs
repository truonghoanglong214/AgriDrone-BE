namespace AgriDrone.IntegrationContracts.Media;

public sealed record MediaUploadRequestV1(
    Guid OperationId,
    Guid TenantId,
    Guid MissionId,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    MediaChecksumV1 Checksum);