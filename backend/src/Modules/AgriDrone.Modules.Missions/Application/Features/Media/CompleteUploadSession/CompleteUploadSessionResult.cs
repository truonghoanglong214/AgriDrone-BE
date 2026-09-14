using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CompleteUploadSession;

public sealed record CompleteUploadSessionResult(
    Guid UploadSessionId,
    Guid MediaAssetId,
    MediaUploadSessionStatus Status,
    string MimeType,
    long FileSizeBytes,
    string Sha256Checksum,
    bool ReusedCompletion);