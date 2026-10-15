using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Missions.Infrastructure.Processing;

internal sealed class MetashapeService(
    HttpClient httpClient,
    IOptions<MetashapeOptions> options,
    ILogger<MetashapeService> logger) : IMetashapeService
{
    private readonly MetashapeOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MetashapeTaskCreatedResult> CreateTaskAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        IEnumerable<MetashapeTaskImageItem> images,
        MetashapeTaskOptions? taskOptions = null,
        CancellationToken cancellationToken = default)
    {
        var imageList = images.ToList();
        if (imageList.Count == 0)
        {
            return new MetashapeTaskCreatedResult(false, null, "No image items provided for Metashape task.");
        }

        var payload = new
        {
            mission_id = missionId.ToString("D"),
            tenant_id = tenantId.ToString("D"),
            farm_id = farmId.ToString("D"),
            images = imageList.Select(img => new
            {
                storage_key = img.StorageKey,
                url = img.Url
            }),
            camera_type = taskOptions?.CameraType ?? _options.CameraType,
            align_accuracy = taskOptions?.AlignAccuracy ?? _options.AlignAccuracy,
            generic_preselection = taskOptions?.GenericPreselection ?? _options.GenericPreselection
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        logger.LogInformation(
            "Submitting Metashape processing task to {Endpoint} with {Count} images for Mission {MissionId}",
            _options.Endpoint,
            imageList.Count,
            missionId);

        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/metashape/process";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Metashape task creation failed with HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    responseBody);

                return new MetashapeTaskCreatedResult(
                    false,
                    null,
                    $"Metashape error ({response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("task_id", out var taskIdElement))
            {
                var taskId = taskIdElement.GetString();
                logger.LogInformation("Metashape task created successfully: {TaskId}", taskId);
                return new MetashapeTaskCreatedResult(true, taskId, null);
            }

            return new MetashapeTaskCreatedResult(false, null, $"Unexpected response format: {responseBody}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit task to Metashape service at {Endpoint}", _options.Endpoint);
            return new MetashapeTaskCreatedResult(false, null, ex.Message);
        }
    }

    public async Task<MetashapeTaskStatusResult> GetTaskStatusAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/metashape/tasks/{taskId}";
            var response = await httpClient.GetAsync(requestUri, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new MetashapeTaskStatusResult(
                    false, taskId, "UNKNOWN", 0, 0, null, 0, null,
                    $"HTTP {response.StatusCode}: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var statusName = root.TryGetProperty("status", out var sElem) ? sElem.GetString() ?? "UNKNOWN" : "UNKNOWN";
            var progress = root.TryGetProperty("progress", out var pElem) ? pElem.GetInt32() : 0;
            var stage = root.TryGetProperty("stage", out var stElem) ? stElem.GetString() : null;
            var processingTime = root.TryGetProperty("processing_time_ms", out var tElem) ? tElem.GetInt64() : 0;
            var errorMessage = root.TryGetProperty("error_message", out var errElem) ? errElem.GetString() : null;

            var statusCode = statusName.ToUpperInvariant() switch
            {
                "QUEUED" => 10,
                "RUNNING" => 20,
                "FAILED" => 30,
                "COMPLETED" => 40,
                "CANCELED" => 50,
                _ => 0
            };

            MetashapeOutputMetadata? output = null;
            if (statusCode == 40)
            {
                var tifStorageKey = root.TryGetProperty("tif_storage_key", out var tskElem) ? tskElem.GetString() : null;
                var jpgStorageKey = root.TryGetProperty("jpg_storage_key", out var jskElem) ? jskElem.GetString() : null;
                var tifUrl = root.TryGetProperty("tif_url", out var tuElem) ? tuElem.GetString() : null;
                var previewUrl = root.TryGetProperty("preview_url", out var puElem) ? puElem.GetString() : null;

                MapBounds? bounds = null;
                if (root.TryGetProperty("bounds", out var bElem) && bElem.ValueKind == JsonValueKind.Object)
                {
                    bounds = new MapBounds(
                        bElem.GetProperty("min_lat").GetDouble(),
                        bElem.GetProperty("max_lat").GetDouble(),
                        bElem.GetProperty("min_lon").GetDouble(),
                        bElem.GetProperty("max_lon").GetDouble());
                }

                MapCorners? corners = null;
                if (root.TryGetProperty("corners", out var cElem) && cElem.ValueKind == JsonValueKind.Object)
                {
                    var nw = cElem.GetProperty("north_west");
                    var ne = cElem.GetProperty("north_east");
                    var se = cElem.GetProperty("south_east");
                    var sw = cElem.GetProperty("south_west");

                    corners = new MapCorners(
                        new MapCoordinate(nw[0].GetDouble(), nw[1].GetDouble()),
                        new MapCoordinate(ne[0].GetDouble(), ne[1].GetDouble()),
                        new MapCoordinate(se[0].GetDouble(), se[1].GetDouble()),
                        new MapCoordinate(sw[0].GetDouble(), sw[1].GetDouble()));
                }

                int? origW = root.TryGetProperty("original_width", out var owElem) && owElem.ValueKind == JsonValueKind.Number ? owElem.GetInt32() : null;
                int? origH = root.TryGetProperty("original_height", out var ohElem) && ohElem.ValueKind == JsonValueKind.Number ? ohElem.GetInt32() : null;
                long? tifSize = root.TryGetProperty("tif_size_bytes", out var tsElem) && tsElem.ValueKind == JsonValueKind.Number ? tsElem.GetInt64() : null;
                long? jpgSize = root.TryGetProperty("jpg_size_bytes", out var jsElem) && jsElem.ValueKind == JsonValueKind.Number ? jsElem.GetInt64() : null;
                string? tifHash = root.TryGetProperty("tif_checksum", out var thElem) ? thElem.GetString() : null;
                string? jpgHash = root.TryGetProperty("jpg_checksum", out var jhElem) ? jhElem.GetString() : null;

                output = new MetashapeOutputMetadata(
                    tifStorageKey,
                    jpgStorageKey,
                    tifUrl,
                    previewUrl,
                    bounds,
                    corners,
                    origW,
                    origH,
                    tifSize,
                    jpgSize,
                    tifHash,
                    jpgHash);
            }

            return new MetashapeTaskStatusResult(
                true,
                taskId,
                statusName,
                statusCode,
                progress,
                stage,
                processingTime,
                output,
                errorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting status for Metashape task {TaskId}", taskId);
            return new MetashapeTaskStatusResult(false, taskId, "ERROR", 0, 0, null, 0, null, ex.Message);
        }
    }

    public async Task<bool> CancelTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/metashape/tasks/{taskId}/cancel";
            using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel Metashape task {TaskId}", taskId);
            return false;
        }
    }
}
