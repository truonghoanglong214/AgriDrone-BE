using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record CreateMissionMediaUploadSessionRequest(
    Guid OperationId,
    string FileName,
    string MimeType,
    MediaType MediaType,
    long FileSizeBytes,
    string Sha256Checksum,
    uint ExpectedMissionVersion);