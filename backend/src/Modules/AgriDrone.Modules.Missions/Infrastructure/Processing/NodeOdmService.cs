using System.Net.Http.Headers;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class NodeOdmService(
    HttpClient httpClient,
    IOptions<NodeOdmOptions> options,
    ILogger<NodeOdmService> logger) : INodeOdmService
{
    private readonly NodeOdmOptions _options = options.Value;

    public async Task<NodeOdmTaskCreatedResult> CreateTaskAsync(
        IEnumerable<string> imageFilePaths,
        NodeOdmTaskOptions? taskOptions = null,
        CancellationToken cancellationToken = default)
    {
        var fileList = imageFilePaths.ToList();
        if (fileList.Count == 0)
        {
            return new NodeOdmTaskCreatedResult(false, null, "No image files provided for NodeODM task.");
        }

        var opt = taskOptions ?? new NodeOdmTaskOptions(
            _options.FeatureQuality,
            _options.ResizeTo,
            _options.PcQuality,
            _options.MaxConcurrency,
            _options.FastOrthophoto);

        // Build NodeODM options array
        var optionsArray = new object[]
        {
            new { name = "feature-quality", value = opt.FeatureQuality },
            new { name = "resize-to", value = opt.ResizeTo },
            new { name = "pc-quality", value = opt.PcQuality },
            new { name = "max-concurrency", value = opt.MaxConcurrency },
            new { name = "fast-orthophoto", value = opt.FastOrthophoto }
        };

        var optionsJson = JsonSerializer.Serialize(optionsArray);

        logger.LogInformation(
            "Submitting task to NodeODM at {Endpoint} with {Count} images and options: {Options}",
            _options.Endpoint,
            fileList.Count,
            optionsJson);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(optionsJson), "options");

        var fileStreams = new List<FileStream>();
        try
        {
            foreach (var filePath in fileList)
            {
                if (!File.Exists(filePath))
                {
                    logger.LogWarning("File not found for NodeODM upload: {Path}", filePath);
                    continue;
                }

                var fs = File.OpenRead(filePath);
                fileStreams.Add(fs);

                var streamContent = new StreamContent(fs);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(streamContent, "images", Path.GetFileName(filePath));
            }

            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/task/new";
            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "NodeODM task/new failed with HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    responseBody);

                return new NodeOdmTaskCreatedResult(
                    false,
                    null,
                    $"NodeODM error ({response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("uuid", out var uuidElement))
            {
                var uuid = uuidElement.GetString();
                logger.LogInformation("NodeODM task created successfully: {TaskId}", uuid);
                return new NodeOdmTaskCreatedResult(true, uuid, null);
            }

            return new NodeOdmTaskCreatedResult(false, null, $"Unexpected NodeODM response: {responseBody}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit task to NodeODM.");
            return new NodeOdmTaskCreatedResult(false, null, ex.Message);
        }
        finally
        {
            foreach (var fs in fileStreams)
            {
                await fs.DisposeAsync();
            }
        }
    }

    public async Task<NodeOdmTaskStatusResult> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/task/{taskId}/info";
            var response = await httpClient.GetAsync(requestUri, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new NodeOdmTaskStatusResult(
                    false, taskId, "UNKNOWN", 0, 0, 0,
                    $"Failed to get task info: HTTP {response.StatusCode} - {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var statusCode = 0;
            if (root.TryGetProperty("status", out var statusObj) &&
                statusObj.TryGetProperty("code", out var codeElem))
            {
                statusCode = codeElem.GetInt32();
            }

            var progress = 0;
            if (root.TryGetProperty("progress", out var progressElem))
            {
                progress = progressElem.GetInt32();
            }

            long processingTime = 0;
            if (root.TryGetProperty("processingTime", out var timeElem))
            {
                processingTime = timeElem.GetInt64();
            }

            var statusName = statusCode switch
            {
                10 => "QUEUED",
                20 => "RUNNING",
                30 => "FAILED",
                40 => "COMPLETED",
                50 => "CANCELED",
                _ => "UNKNOWN"
            };

            return new NodeOdmTaskStatusResult(
                true, taskId, statusName, statusCode, progress, processingTime, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting status for NodeODM task {TaskId}", taskId);
            return new NodeOdmTaskStatusResult(false, taskId, "ERROR", 0, 0, 0, ex.Message);
        }
    }

    public async Task<Stream?> DownloadAssetAsync(
        string taskId,
        string assetName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/task/{taskId}/download/{assetName}";
            var response = await httpClient.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Download asset {Asset} for task {TaskId} returned HTTP {StatusCode}",
                    assetName,
                    taskId,
                    response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStreamAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download asset {Asset} for NodeODM task {TaskId}", assetName, taskId);
            return null;
        }
    }

    public async Task<bool> CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/task/cancel";
            var payload = JsonSerializer.Serialize(new { uuid = taskId });
            using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel NodeODM task {TaskId}", taskId);
            return false;
        }
    }
}
