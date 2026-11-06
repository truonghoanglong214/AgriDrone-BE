using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionUploadReadiness;
using AgriDrone.Modules.Missions.Application.Features.Media.ResetMissionUpload;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application
    .Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MissionUploadController(
    ISender sender,
    ISystemManagerAccessService managerAccessService)
    : ControllerBase
{
    /// <summary>Reset toàn bộ ảnh đã upload của Mission để nạp lại từ đầu.</summary>
    /// <remarks>
    /// Áp dụng khi Mission ở trạng thái Uploading, UploadFailed, ReadyForProcessing hoặc ProcessingFailed (ví dụ up nhầm ảnh không khớp Blackbox dẫn đến Geotag thất bại).
    /// Endpoint sẽ xóa file ảnh trên MinIO, xóa dữ liệu Media trong DB và đưa Mission trở về trạng thái Uploading.
    /// Tùy chọn ResetTelemetry = true để xóa luôn Blackbox nếu muốn nhập lại cả log bay.
    /// </remarks>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/upload/reset")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ProducesResponseType(typeof(ResetMissionUploadResponse), StatusCodes.Status200OK)]
    public async Task<IResult> ResetMissionUpload(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromBody] ResetMissionUploadRequest? request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var req = request ?? new ResetMissionUploadRequest();
        var command = new ResetMissionUploadCommand(
            tenantId,
            farmId,
            missionId,
            req.ExpectedMissionVersion,
            req.ResetTelemetry);

        var result = await sender.Send(command, cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new ResetMissionUploadResponse(
                    value.MissionId,
                    value.Status.ToString(),
                    value.DeletedMediaCount,
                    value.TelemetryReset,
                    value.MissionVersion)));
    }

    /// <summary>Hoàn tất giai đoạn upload của Mission.</summary>
    /// <remarks>
    /// Chỉ thành công khi Mission đang Uploading, không còn upload session hoạt
    /// động, có tối thiểu một media hợp lệ và telemetry đã import từ hai điểm
    /// trở lên. Backend kiểm tra flight route có SRID 4326 rồi chuyển Mission
    /// sang ReadyForProcessing để bắt đầu xử lý AI.
    /// </remarks>
    [HttpPost("api/missions/{missionId:guid}/farms/{farmId:guid}/upload/finalize")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> FinalizeMissionUpload(
        [FromRoute] Guid farmId,
        Guid missionId,
        [FromBody] FinalizeMissionUploadRequest request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
        {
            return Results.Forbid();
        }

        var command = new FinalizeMissionUploadCommand(
            tenantId,
            farmId,
            missionId,
            request.ExpectedMissionVersion);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new FinalizeMissionUploadResponse(
                    value.MissionId,
                    value.Status.ToString(),
                    value.MediaCount,
                    value.TelemetryPointCount,
                    value.MissionVersion)));
    }

    /// <summary>Kiểm tra điều kiện hoàn tất upload của Mission.</summary>
    /// <remarks>Trả CanFinalize và danh sách Blockers, cùng số media, điểm telemetry và tình trạng upload session. Dùng kết quả để xử lý dữ liệu còn thiếu trước khi gọi upload/finalize.</remarks>
    [HttpGet("api/missions/{missionId:guid}/farms/{farmId:guid}/upload/readiness")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(
        typeof(MissionUploadReadinessResponse),
        StatusCodes.Status200OK)]
    public async Task<IResult> GetUploadReadiness(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is null || access.FarmId != farmId)
            return Results.Forbid();

        var result = await sender.Send(
            new GetMissionUploadReadinessQuery(farmId, missionId),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(new MissionUploadReadinessResponse(
                value.MissionId,
                value.MissionVersion,
                value.Status.ToString(),
                value.CanFinalize,
                value.RawImageCount,
                value.RawVideoCount,
                value.TelemetryPointCount,
                value.HasActiveUploadSessions,
                value.Blockers.Select(error => error.Code).ToArray())));
    }

}
