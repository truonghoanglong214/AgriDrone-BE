using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application
    .Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
public sealed class MissionUploadController(
    ISender sender,
    IAuthorizationService authorizationService,
    IExecutionContext executionContext)
    : ControllerBase
{
    /// <summary>Hoàn tất giai đoạn upload của Mission.</summary>
    /// <remarks>
    /// Chỉ thành công khi Mission đang Uploading, không còn upload session hoạt
    /// động, có tối thiểu một media hợp lệ và telemetry đã import từ hai điểm
    /// trở lên. Backend kiểm tra flight route có SRID 4326 rồi chuyển Mission
    /// sang ReadyForProcessing để bắt đầu xử lý AI.
    /// </remarks>
    [HttpPost(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}/upload/finalize")]
    public async Task<IResult> FinalizeMissionUpload(
        Guid farmId,
        Guid missionId,
        [FromBody] FinalizeMissionUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Results.Unauthorized();
        }

        var authorization =
            await authorizationService.AuthorizeAsync(
                User,
                new FarmAccessTarget(
                    tenantId,
                    farmId),
                AccessAuthorizationPolicies.FarmManage);

        if (!authorization.Succeeded)
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
}
