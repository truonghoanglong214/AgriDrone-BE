using AgriDrone.Modules.Missions.Application.Abstractions.Media;

namespace AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;

public sealed record StartMultipartUploadResult(Guid UploadSessionId,
    long PartSizeBytes, int PartCount, DateTimeOffset ExpiresAt,
    IReadOnlyList<MultipartUploadedPart> UploadedParts, bool ReusedUpload);
