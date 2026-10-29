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
    /// - Camera type: equidistant_fisheye
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
}
