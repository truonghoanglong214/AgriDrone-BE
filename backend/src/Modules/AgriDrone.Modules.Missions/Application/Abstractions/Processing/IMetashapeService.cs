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

    Task<MetashapeTaskCreatedResult> CreateTaskAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        IEnumerable<MetashapeTaskImageItem> images,
        MetashapeTaskOptions? options,
        string engine,
        CancellationToken cancellationToken = default);

    Task<MetashapeTaskStatusResult> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<bool> CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<SliceTilesResult> SliceTilesAsync(
        Guid missionId,
        Guid tenantId,
        string? tifStorageKey = null,
        int tileSize = 640,
        int overlap = 64,
        bool dualScale = true,
        object? polygonGeoJson = null,
        bool uploadToMinio = false,
        CancellationToken cancellationToken = default);

    Task<DetectMissionTreesResult> DetectMissionTreesAsync(
        Guid missionId,
        Guid tenantId,
        double confidenceThreshold = 0.25,
        double rowSpacing = 2.8,
        double colSpacing = 2.5,
        object? polygonGeoJson = null,
        double? anchorLat = null,
        double? anchorLon = null,
        double? customAngleDeg = null,
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string FileName)?> DownloadTreesCsvAsync(
        Guid missionId,
        CancellationToken cancellationToken = default);

    Task<DetectMissionDiseasesResult> DetectMissionDiseasesAsync(
        Guid missionId,
        Guid tenantId,
        IEnumerable<MetashapeTaskImageItem>? images = null,
        double confidenceThreshold = 0.25,
        object? polygonGeoJson = null,
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string FileName)?> DownloadDiseasesCsvAsync(
        Guid missionId,
        CancellationToken cancellationToken = default);
}
