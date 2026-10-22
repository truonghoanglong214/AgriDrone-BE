using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record CreateMissionMediaUploadSessionRequest(
    Guid IdempotencyKey,
    string FileName,
    string MimeType,
    MediaType MediaType,
    long FileSizeBytes,
    string Sha256Checksum,
    uint ExpectedMissionVersion,
    DateTimeOffset? CapturedAt = null);
