using AgriDrone.Modules.Missions.Domain.Media;

namespace AgriDrone.Modules.Missions.Application.Features.Media;

public sealed record MissionMediaResponse(
    Guid MediaId,
    Guid MissionId,
    MediaType MediaType,
    MissionMediaRole MediaRole,
    string? MimeType,
    long? FileSizeBytes,
    string? Sha256Checksum,
    int? WidthPx,
    int? HeightPx,
    long? DurationMs,
    DateTimeOffset? CapturedAt,
    long? TelemetryTimeOffsetMs,
    string? CaptureClockSource,
    DateTimeOffset CreatedAt);
