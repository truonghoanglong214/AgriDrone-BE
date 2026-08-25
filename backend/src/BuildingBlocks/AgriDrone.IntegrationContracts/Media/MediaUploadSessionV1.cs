namespace AgriDrone.IntegrationContracts.Media;

public sealed record MediaUploadSessionV1(
    Guid UploadSessionId,
    Guid MediaAssetId,
    string UploadUri,
    DateTimeOffset ExpiresAt);