namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public sealed record MetashapeTaskOptions(
    string? CameraType = null,
    string? AlignAccuracy = null,
    bool? GenericPreselection = null);

public sealed record MetashapeTaskImageItem(
    string StorageKey,
    string? Url = null);

public sealed record MetashapeTaskCreatedResult(
    bool IsSuccess,
    string? TaskId,
    string? ErrorMessage);

public sealed record MetashapeOutputMetadata(
    string? TifStorageKey,
    string? JpgStorageKey,
    string? TifUrl,
    string? PreviewUrl,
    MapBounds? Bounds,
    MapCorners? Corners,
    int? OriginalWidth,
    int? OriginalHeight,
    long? TifSizeBytes,
    long? JpgSizeBytes,
    string? TifChecksum,
    string? JpgChecksum);

public sealed record MetashapeTaskStatusResult(
    bool IsSuccess,
    string TaskId,
    string StatusName,
    int StatusCode,
    int Progress,
    string? Stage,
    long ProcessingTimeMs,
    MetashapeOutputMetadata? Output,
    string? ErrorMessage);

public interface IMetashapeService
{
    Task<MetashapeTaskCreatedResult> CreateTaskAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        IEnumerable<MetashapeTaskImageItem> images,
        MetashapeTaskOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<MetashapeTaskStatusResult> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<bool> CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);
}
