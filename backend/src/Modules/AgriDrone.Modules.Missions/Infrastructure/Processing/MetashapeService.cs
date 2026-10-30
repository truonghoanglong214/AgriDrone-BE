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

    public Task<MetashapeTaskCreatedResult> CreateTaskAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        IEnumerable<MetashapeTaskImageItem> images,
        MetashapeTaskOptions? taskOptions = null,
        CancellationToken cancellationToken = default)
        => CreateTaskAsync(missionId, tenantId, farmId, images, taskOptions, "metashape", cancellationToken);

    public async Task<MetashapeTaskCreatedResult> CreateTaskAsync(
        Guid missionId,
        Guid tenantId,
        Guid farmId,
        IEnumerable<MetashapeTaskImageItem> images,
        MetashapeTaskOptions? taskOptions,
        string engine,
        CancellationToken cancellationToken = default)
    {
        var imageList = images.ToList();
        if (imageList.Count == 0)
        {
            return new MetashapeTaskCreatedResult(false, null, $"No image items provided for {engine} task.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["mission_id"] = missionId.ToString("D"),
            ["tenant_id"] = tenantId.ToString("D"),
            ["farm_id"] = farmId.ToString("D"),
            ["images"] = imageList.Select(img => new
            {
                storage_key = img.StorageKey,
                url = img.Url
            })
        };

        if (!string.IsNullOrWhiteSpace(taskOptions?.CameraType))
        {
            payload["camera_type"] = taskOptions.CameraType;
        }

        if (!string.IsNullOrWhiteSpace(taskOptions?.AlignAccuracy))
        {
            payload["align_accuracy"] = taskOptions.AlignAccuracy;
        }

        if (taskOptions?.GenericPreselection.HasValue == true)
        {
            payload["generic_preselection"] = taskOptions.GenericPreselection.Value;
        }

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        logger.LogInformation(
            "Submitting {Engine} processing task to {Endpoint} with {Count} images for Mission {MissionId}",
            engine,
            _options.Endpoint,
            imageList.Count,
            missionId);

        try
        {
            var endpointPath = engine.Equals("odm", StringComparison.OrdinalIgnoreCase)
                ? "/api/v1/odm/process"
                : "/api/v1/metashape/process";
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}{endpointPath}";
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

    public async Task<SliceTilesResult> SliceTilesAsync(
        Guid missionId,
        Guid tenantId,
        string? tifStorageKey = null,
        int tileSize = 640,
        int overlap = 64,
        bool dualScale = true,
        object? polygonGeoJson = null,
        bool uploadToMinio = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                mission_id = missionId.ToString("D"),
                tenant_id = tenantId.ToString("D"),
                tif_storage_key = tifStorageKey,
                tile_size = tileSize,
                overlap,
                dual_scale = dualScale,
                polygon_geojson = polygonGeoJson,
                upload_to_minio = uploadToMinio
            };

            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/tiling/slice";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Tiling slice failed with HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    responseBody);

                return new SliceTilesResult(
                    false,
                    missionId,
                    0,
                    0,
                    0,
                    dualScale,
                    Array.Empty<TileSliceItem>(),
                    $"Tiling slice error ({response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var totalTiles = root.TryGetProperty("total_tiles", out var ttElem) ? ttElem.GetInt32() : 0;
            var total640 = root.TryGetProperty("total_tiles_640", out var t640Elem) ? t640Elem.GetInt32() : 0;
            var total1280 = root.TryGetProperty("total_tiles_1280", out var t1280Elem) ? t1280Elem.GetInt32() : 0;
            var dual = root.TryGetProperty("dual_scale", out var dsElem) && dsElem.GetBoolean();

            var tileItems = new List<TileSliceItem>();
            if (root.TryGetProperty("tiles", out var tilesElem) && tilesElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tilesElem.EnumerateArray())
                {
                    var idx = t.GetProperty("tile_index").GetInt32();
                    int? tSize = t.TryGetProperty("tile_size", out var ts) && ts.ValueKind == JsonValueKind.Number ? ts.GetInt32() : null;
                    var colOff = t.GetProperty("col_off").GetInt32();
                    var rowOff = t.GetProperty("row_off").GetInt32();
                    var width = t.GetProperty("width").GetInt32();
                    var height = t.GetProperty("height").GetInt32();
                    var fileName = t.GetProperty("file_name").GetString() ?? string.Empty;
                    var storageKey = t.TryGetProperty("storage_key", out var sk) ? sk.GetString() : null;
                    var url = t.TryGetProperty("url", out var u) ? u.GetString() : null;

                    var b = t.GetProperty("bounds");
                    var bounds = new MapBounds(
                        b.GetProperty("min_lat").GetDouble(),
                        b.GetProperty("max_lat").GetDouble(),
                        b.GetProperty("min_lon").GetDouble(),
                        b.GetProperty("max_lon").GetDouble());

                    var c = t.GetProperty("corners");
                    var nw = c.GetProperty("north_west");
                    var ne = c.GetProperty("north_east");
                    var se = c.GetProperty("south_east");
                    var sw = c.GetProperty("south_west");
                    var corners = new MapCorners(
                        new MapCoordinate(nw[0].GetDouble(), nw[1].GetDouble()),
                        new MapCoordinate(ne[0].GetDouble(), ne[1].GetDouble()),
                        new MapCoordinate(se[0].GetDouble(), se[1].GetDouble()),
                        new MapCoordinate(sw[0].GetDouble(), sw[1].GetDouble()));

                    var affine = new List<double>();
                    if (t.TryGetProperty("affine_transform", out var affElem) && affElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var val in affElem.EnumerateArray())
                        {
                            affine.Add(val.GetDouble());
                        }
                    }

                    tileItems.Add(new TileSliceItem(
                        idx, tSize, colOff, rowOff, width, height, fileName, storageKey, url, bounds, corners, affine));
                }
            }

            return new SliceTilesResult(
                true,
                missionId,
                totalTiles,
                total640,
                total1280,
                dual,
                tileItems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run tiling slice for mission {MissionId}", missionId);
            return new SliceTilesResult(
                false,
                missionId,
                0,
                0,
                0,
                dualScale,
                Array.Empty<TileSliceItem>(),
                ex.Message);
        }
    }

    public async Task<DetectMissionTreesResult> DetectMissionTreesAsync(
        Guid missionId,
        Guid tenantId,
        double confidenceThreshold = 0.25,
        double rowSpacing = 2.8,
        double colSpacing = 2.5,
        object? polygonGeoJson = null,
        double? anchorLat = null,
        double? anchorLon = null,
        double? customAngleDeg = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                mission_id = missionId.ToString("D"),
                tenant_id = tenantId.ToString("D"),
                confidence_threshold = confidenceThreshold,
                row_spacing = rowSpacing,
                col_spacing = colSpacing,
                anchor_lat = anchorLat,
                anchor_lon = anchorLon,
                custom_angle_deg = customAngleDeg,
                polygon_geojson = polygonGeoJson
            };

            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/yolo/detect-mission";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "YOLO detection failed with HTTP {Status}: {Body}",
                    (int)response.StatusCode,
                    responseBody);

                return new DetectMissionTreesResult(
                    false,
                    missionId,
                    string.Empty,
                    0, 0, 0, 0, null, null, 0, 0, null,
                    Array.Empty<TreeDetectionItem>(),
                    Array.Empty<TreeDetectionItem>(),
                    $"YOLO detection error ({response.StatusCode}): {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var modelName = root.TryGetProperty("model_name", out var mn) ? mn.GetString() ?? "" : "";
            var totalScanned = root.TryGetProperty("total_tiles_scanned", out var ts) ? ts.GetInt32() : 0;
            var totalRaw = root.TryGetProperty("total_raw_detections", out var tr) ? tr.GetInt32() : 0;
            var totalUnique = root.TryGetProperty("total_unique_trees", out var tu) ? tu.GetInt32() : 0;
            var totalMissing = root.TryGetProperty("total_missing_trees", out var tm) ? tm.GetInt32() : 0;
            double? sugRow = root.TryGetProperty("suggested_row_spacing", out var sr) && sr.ValueKind == JsonValueKind.Number ? sr.GetDouble() : null;
            double? sugCol = root.TryGetProperty("suggested_col_spacing", out var sc) && sc.ValueKind == JsonValueKind.Number ? sc.GetDouble() : null;
            var maxRow = root.TryGetProperty("max_row", out var mr) ? mr.GetInt32() : 0;
            var maxCol = root.TryGetProperty("max_col", out var mc) ? mc.GetInt32() : 0;
            var csvUrl = root.TryGetProperty("csv_download_url", out var cu) ? cu.GetString() : null;

            static List<TreeDetectionItem> ParseTrees(JsonElement element)
            {
                var list = new List<TreeDetectionItem>();
                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetString() ?? "";
                        var row = item.GetProperty("row").GetInt32();
                        var col = item.GetProperty("col").GetInt32();
                        var lat = item.GetProperty("lat").GetDouble();
                        var lon = item.GetProperty("lon").GetDouble();
                        var conf = item.TryGetProperty("confidence", out var c) ? c.GetDouble() : 1.0;
                        var st = item.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                        var src = item.TryGetProperty("source", out var srcElem) ? srcElem.GetString() ?? "" : "";
                        var isAnchor = item.TryGetProperty("is_anchor", out var a) && a.GetBoolean();

                        list.Add(new TreeDetectionItem(id, row, col, lat, lon, conf, st, src, isAnchor));
                    }
                }
                return list;
            }

            var activeTrees = root.TryGetProperty("active_trees", out var atElem)
                ? ParseTrees(atElem)
                : new List<TreeDetectionItem>();

            var missingTrees = root.TryGetProperty("missing_trees", out var mtElem)
                ? ParseTrees(mtElem)
                : new List<TreeDetectionItem>();

            return new DetectMissionTreesResult(
                true,
                missionId,
                modelName,
                totalScanned,
                totalRaw,
                totalUnique,
                totalMissing,
                sugRow,
                sugCol,
                maxRow,
                maxCol,
                csvUrl,
                activeTrees,
                missingTrees);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run YOLO tree detection for mission {MissionId}", missionId);
            return new DetectMissionTreesResult(
                false,
                missionId,
                string.Empty,
                0, 0, 0, 0, null, null, 0, 0, null,
                Array.Empty<TreeDetectionItem>(),
                Array.Empty<TreeDetectionItem>(),
                ex.Message);
        }
    }

    public async Task<(byte[] Bytes, string FileName)?> DownloadTreesCsvAsync(
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/yolo/download-csv/{missionId:D}";
            var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Download CSV failed with status {Status} for mission {MissionId}",
                    response.StatusCode,
                    missionId);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (bytes, $"trees_{missionId:D}.csv");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download trees CSV for mission {MissionId}", missionId);
            return null;
        }
    }

    public async Task<DetectMissionDiseasesResult> DetectMissionDiseasesAsync(
        Guid missionId,
        Guid tenantId,
        IEnumerable<MetashapeTaskImageItem>? images = null,
        double confidenceThreshold = 0.25,
        object? polygonGeoJson = null,
        CancellationToken cancellationToken = default)
    {
        var imageList = images?.ToList() ?? new List<MetashapeTaskImageItem>();
        var payload = new
        {
            mission_id = missionId.ToString("D"),
            tenant_id = tenantId.ToString("D"),
            confidence_threshold = confidenceThreshold,
            polygon_geojson = polygonGeoJson,
            images = imageList.Select(img => new
            {
                storage_key = img.StorageKey,
                url = img.Url
            })
        };

        var json = JsonSerializer.Serialize(payload, JsonOpts);
        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/disease/detect-mission";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(requestUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Disease detection failed with HTTP {Status}: {Body}", (int)response.StatusCode, responseBody);
                return new DetectMissionDiseasesResult(
                    false, missionId, string.Empty, 0, 0, new Dictionary<string, int>(), null,
                    Array.Empty<DiseaseDetectionItem>(), null, $"HTTP {response.StatusCode}: {responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var modelName = root.TryGetProperty("model_name", out var mn) ? mn.GetString() ?? "" : "";
            var totalScanned = root.TryGetProperty("total_images_scanned", out var ts) ? ts.GetInt32() : 0;
            var totalDetections = root.TryGetProperty("total_detections", out var td) ? td.GetInt32() : 0;
            var csvUrl = root.TryGetProperty("csv_download_url", out var cu) ? cu.GetString() : null;

            var summary = new Dictionary<string, int>();
            if (root.TryGetProperty("disease_summary", out var ds) && ds.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in ds.EnumerateObject())
                {
                    summary[prop.Name] = prop.Value.GetInt32();
                }
            }

            var detections = new List<DiseaseDetectionItem>();
            if (root.TryGetProperty("detections", out var dt) && dt.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dt.EnumerateArray())
                {
                    detections.Add(new DiseaseDetectionItem(
                        item.GetProperty("id").GetString() ?? "",
                        item.GetProperty("class_id").GetInt32(),
                        item.GetProperty("class_name").GetString() ?? "",
                        item.GetProperty("lat").GetDouble(),
                        item.GetProperty("lon").GetDouble(),
                        item.GetProperty("confidence").GetDouble(),
                        item.GetProperty("bbox_x").GetDouble(),
                        item.GetProperty("bbox_y").GetDouble(),
                        item.GetProperty("bbox_w").GetDouble(),
                        item.GetProperty("bbox_h").GetDouble(),
                        item.GetProperty("image_name").GetString() ?? "",
                        item.TryGetProperty("crop_image_url", out var ciu) ? ciu.GetString() : null,
                        item.TryGetProperty("annotated_image_url", out var aiu) ? aiu.GetString() : null));
                }
            }

            var annotatedUrls = new List<string>();
            if (root.TryGetProperty("annotated_image_urls", out var aurls) && aurls.ValueKind == JsonValueKind.Array)
            {
                foreach (var u in aurls.EnumerateArray())
                {
                    var s = u.GetString();
                    if (s != null) annotatedUrls.Add(s);
                }
            }

            return new DetectMissionDiseasesResult(
                true, missionId, modelName, totalScanned, totalDetections, summary, csvUrl, detections, annotatedUrls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run disease detection for mission {MissionId}", missionId);
            return new DetectMissionDiseasesResult(
                false, missionId, string.Empty, 0, 0, new Dictionary<string, int>(), null,
                Array.Empty<DiseaseDetectionItem>(), null, ex.Message);
        }
    }

    public async Task<(byte[] Bytes, string FileName)?> DownloadDiseasesCsvAsync(
        Guid missionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestUri = $"{_options.Endpoint.TrimEnd('/')}/api/v1/disease/download-csv/{missionId:D}";
            var response = await httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Download disease CSV failed with status {Status} for mission {MissionId}",
                    response.StatusCode,
                    missionId);
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return (bytes, $"diseases_{missionId:D}.csv");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download diseases CSV for mission {MissionId}", missionId);
            return null;
        }
    }
}
