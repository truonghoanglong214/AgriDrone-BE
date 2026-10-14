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
    /// Kích hoạt gửi ảnh đã gắn GPS sang NodeODM để ghép bản đồ (bước riêng biệt do người dùng chủ động gọi).
    /// </summary>
    /// <remarks>
    /// Mặc định sử dụng bộ cấu hình chống tràn RAM:
    /// - feature-quality: lowest
    /// - resize-to: 1200
    /// - pc-quality: lowest
    /// - max-concurrency: 4
    /// - fast-orthophoto: true
    /// </remarks>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/process-nodeodm")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> ProcessNodeOdm(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] ProcessNodeOdmRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        NodeOdmTaskOptions? customOptions = null;
        if (request is not null &&
            (request.CameraLens is not null ||
             request.FeatureQuality is not null ||
             request.ResizeTo is not null ||
             request.PcQuality is not null ||
             request.MaxConcurrency is not null ||
             request.FastOrthophoto is not null))
        {
            customOptions = new NodeOdmTaskOptions(
                request.CameraLens,
                request.FeatureQuality,
                request.ResizeTo,
                request.PcQuality,
                request.MaxConcurrency,
                request.FastOrthophoto);
        }

        var command = new ProcessNodeOdmCommand(
            tenantId,
            farmId,
            missionId,
            customOptions);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new ProcessNodeOdmResponse(
                value.MissionId,
                value.TaskId,
                value.Status,
                value.Message)));
    }

    /// <summary>
    /// Kiểm tra tiến độ ghép bản đồ của NodeODM và lấy ảnh xem trước nhẹ (map_preview.jpg) kèm tọa độ 4 góc GPS khi hoàn thành.
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
