using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Application.Features.Processing;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MissionOrthophotoController(
    ISender sender,
    ISystemManagerAccessService managerAccessService)
    : ControllerBase
{
    /// <summary>
    /// Kích hoạt gửi ảnh đã gắn GPS sang Agisoft Metashape Pro để ghép bản đồ (bước riêng biệt do người dùng chủ động gọi).
    /// </summary>
    /// <remarks>
    /// Cấu hình tối ưu cho camera GoPro 5 (Equidistant Fisheye):
    /// - Camera type: Mặc định dùng camera góc Linear ('frame' / 'perspective'). Có thể truyền 'equidistant_fisheye' nếu chụp bằng góc Wide.
    /// - Align Accuracy: medium
    /// - Generic preselection: true
    /// - Build Model: Height Field (Tie points)
    /// - Build Orthomosaic: WGS 84 / EPSG:4326 (Mesh)
    /// - Xuất GeoTIFF chuẩn LZW và ảnh JPEG phẳng chất lượng 100%.
    /// </remarks>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/process-metashape")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> ProcessMetashape(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] ProcessMetashapeRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        MetashapeTaskOptions? customOptions = null;
        if (request is not null &&
            (request.CameraType is not null ||
             request.AlignAccuracy is not null ||
             request.GenericPreselection is not null))
        {
            customOptions = new MetashapeTaskOptions(
                request.CameraType,
                request.AlignAccuracy,
                request.GenericPreselection);
        }

        var command = new ProcessMetashapeCommand(
            tenantId,
            farmId,
            missionId,
            customOptions);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new ProcessMetashapeResponse(
                value.MissionId,
                value.TaskId,
                value.Status,
                value.Message)));
    }

    /// <summary>
    /// Kích hoạt gửi ảnh đã gắn GPS sang OpenDroneMap (NodeODM) mã nguồn mở để ghép bản đồ (Phương án thay thế độc lập).
    /// Mặc định dùng camera góc Linear ('frame' / 'perspective'). Có thể truyền 'equidistant_fisheye' nếu chụp bằng góc Wide.
    /// </summary>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/process-odm")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> ProcessOdm(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] ProcessMetashapeRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        MetashapeTaskOptions? customOptions = null;
        if (request is not null &&
            (request.CameraType is not null ||
             request.AlignAccuracy is not null ||
             request.GenericPreselection is not null))
        {
            customOptions = new MetashapeTaskOptions(
                request.CameraType,
                request.AlignAccuracy,
                request.GenericPreselection);
        }

        var command = new ProcessMetashapeCommand(
            tenantId,
            farmId,
            missionId,
            customOptions,
            Engine: "odm");

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new ProcessMetashapeResponse(
                value.MissionId,
                value.TaskId,
                value.Status,
                value.Message)));
    }

    /// <summary>
    /// Kiểm tra tiến độ ghép bản đồ của Metashape / ODM và lấy ảnh xem trước nhẹ (map_preview.jpg) kèm tọa độ 4 góc GPS khi hoàn thành.
    /// </summary>
    [HttpGet("api/missions/{missionId:guid}/farms/{farmId:guid}/orthophoto-status")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IResult> GetOrthophotoStatus(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var query = new GetMissionOrthophotoStatusQuery(tenantId, farmId, missionId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new MissionOrthophotoStatusResponse(
                value.MissionId,
                value.TaskId,
                value.Status,
                value.Progress,
                value.ProcessingTimeMs,
                value.PreviewUrl,
                value.TifUrl,
                value.Bounds,
                value.Corners,
                value.ErrorMessage)));
    }

    /// <summary>
    /// Cắt ảnh bản đồ trực giao (GeoTIFF) thành các mảnh tiles tỉ lệ kép (640x640 và 1280x1280) phục vụ nhận diện AI.
    /// </summary>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/tiling-slice")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> SliceTiles(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] SliceMissionTilesRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var req = request ?? new SliceMissionTilesRequest();
        var command = new SliceMissionTilesCommand(
            tenantId,
            farmId,
            missionId,
            req.TileSize,
            req.Overlap,
            req.DualScale,
            req.PolygonGeoJson,
            req.TifStorageKey,
            req.UploadToMinio);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new SliceMissionTilesResponse(
                value.MissionId,
                value.TotalTiles,
                value.TotalTiles640,
                value.TotalTiles1280,
                value.DualScale,
                value.Tiles.Select(t => new MissionTileItemResponse(
                    t.TileIndex,
                    t.TileSize,
                    t.ColOff,
                    t.RowOff,
                    t.Width,
                    t.Height,
                    t.FileName,
                    t.StorageKey,
                    t.Url,
                    t.Bounds,
                    t.Corners,
                    t.AffineTransform)).ToList())));
    }

    /// <summary>
    /// Quét toàn bộ tiles bằng mô hình YOLOv11 để đếm và gắn tọa độ GPS cho từng cây trồng, đồng thời phát hiện cây bị bỏ sót.
    /// </summary>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/yolo-detect")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> DetectTrees(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] DetectMissionTreesRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var req = request ?? new DetectMissionTreesRequest();
        var command = new DetectMissionTreesCommand(
            tenantId,
            farmId,
            missionId,
            req.ConfidenceThreshold,
            req.RowSpacing,
            req.ColSpacing,
            req.AnchorLat,
            req.AnchorLon,
            req.CustomAngleDeg,
            req.PolygonGeoJson);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new DetectMissionTreesResponse(
                value.MissionId,
                value.ModelName,
                value.TotalTilesScanned,
                value.TotalRawDetections,
                value.TotalUniqueTrees,
                value.TotalMissingTrees,
                value.SuggestedRowSpacing,
                value.SuggestedColSpacing,
                value.MaxRow,
                value.MaxCol,
                value.CsvDownloadUrl,
                value.ActiveTrees.Select(t => new DetectedTreeItemResponse(
                    t.Id, t.Row, t.Col, t.Lat, t.Lon, t.Confidence, t.Status, t.Source, t.IsAnchor)).ToList(),
                value.MissingTrees.Select(t => new DetectedTreeItemResponse(
                    t.Id, t.Row, t.Col, t.Lat, t.Lon, t.Confidence, t.Status, t.Source, t.IsAnchor)).ToList())));
    }

    /// <summary>
    /// Tải file CSV chứa toàn bộ tọa độ GPS và hàng/cột của các cây đã nhận diện.
    /// </summary>
    [HttpGet("api/missions/{missionId:guid}/farms/{farmId:guid}/yolo-csv")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> DownloadTreesCsv(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var query = new DownloadMissionTreesCsvQuery(tenantId, farmId, missionId);
        var result = await sender.Send(query, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.File(value.Bytes, "text/csv", value.FileName));
    }
    /// <summary>
    /// Quét các ảnh thô đã gắn GPS bằng mô hình AI nhận diện bệnh (best_disease.pt),
    /// vẽ khung viền vùng bệnh trên ảnh (annotated images), trích xuất tọa độ GPS và xuất CSV.
    /// </summary>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/disease-detect")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> DetectDiseases(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] DetectMissionDiseasesRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var req = request ?? new DetectMissionDiseasesRequest();

        object? polygon = null;
        if (req.PolygonGeoJson is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                polygon = je;
            }
            else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var s = je.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s) &&
                    !s.Equals("string", StringComparison.OrdinalIgnoreCase) &&
                    !s.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(s);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            polygon = doc.RootElement.Clone();
                        }
                    }
                    catch
                    {
                        polygon = null;
                    }
                }
            }
        }

        var command = new DetectMissionDiseasesCommand(
            tenantId,
            farmId,
            missionId,
            req.ConfidenceThreshold,
            polygon);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new DetectMissionDiseasesResponse(
                value.MissionId,
                value.ModelName,
                value.TotalImagesScanned,
                value.TotalDetections,
                value.DiseaseSummary,
                value.CsvDownloadUrl,
                value.Detections.Select(d => new DiseaseDetectionItemResponse(
                    d.Id,
                    d.ClassId,
                    d.ClassName,
                    d.Lat,
                    d.Lon,
                    d.Confidence,
                    d.BboxX,
                    d.BboxY,
                    d.BboxW,
                    d.BboxH,
                    d.ImageName,
                    d.CropImageUrl,
                    d.AnnotatedImageUrl)).ToList(),
                value.AnnotatedImageUrls)));
    }
}
