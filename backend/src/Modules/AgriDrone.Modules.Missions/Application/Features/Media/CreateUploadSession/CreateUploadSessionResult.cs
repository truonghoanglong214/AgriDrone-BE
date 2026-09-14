using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;

public sealed record CreateUploadSessionResult(
    Guid UploadSessionId,
    Guid MediaAssetId,
    Uri UploadUri,
    DateTimeOffset ExpiresAt,
    MediaUploadSessionStatus Status,
    uint MissionVersion,
    bool ReusedOperation);