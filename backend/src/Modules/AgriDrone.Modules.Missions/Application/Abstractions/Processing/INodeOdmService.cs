namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public sealed record NodeOdmTaskOptions(
    string? CameraLens = null,
    string? FeatureQuality = null,
    int? ResizeTo = null,
    string? PcQuality = null,
    int? MaxConcurrency = null,
    bool? FastOrthophoto = null);

public sealed record NodeOdmTaskCreatedResult(
    bool IsSuccess,
    string? TaskId,
    string? ErrorMessage);

public sealed record NodeOdmTaskStatusResult(
    bool IsSuccess,
    string TaskId,
    string StatusName,
    int StatusCode,
    int Progress,
    long ProcessingTimeMs,
    string? ErrorMessage);

public interface INodeOdmService
{
    Task<NodeOdmTaskCreatedResult> CreateTaskAsync(
        IEnumerable<string> imageFilePaths,
        NodeOdmTaskOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<NodeOdmTaskStatusResult> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<Stream?> DownloadAssetAsync(
        string taskId,
        string assetName,
        CancellationToken cancellationToken = default);

    Task<bool> CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);
}
