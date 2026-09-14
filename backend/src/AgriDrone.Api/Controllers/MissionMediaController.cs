using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.CompleteUploadSession;
using AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;
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
public sealed class MissionMediaController(
    ISender sender,
    IAuthorizationService authorizationService,
    IExecutionContext executionContext)
    : ControllerBase
{
    /// <summary>Tạo phiên tải lên media cho Mission.</summary>
    /// <remarks>
    /// Farm Manager tạo upload session idempotent bằng OperationId. API trả về
    /// presigned URL để client tải file trực tiếp lên MinIO; backend không nhận
    /// nội dung file trong request này. Chỉ chấp nhận ảnh JPEG/PNG hoặc video
    /// MP4/MOV theo MediaType, checksum SHA-256 và ExpectedMissionVersion.
    /// </remarks>
    [HttpPost(
        "api/farms/{farmId:guid}/missions/" +
        "{missionId:guid}/media/upload-sessions")]
    public async Task<IResult> CreateUploadSession(
        Guid farmId,
        Guid missionId,
        [FromBody]
        CreateMissionMediaUploadSessionRequest request,
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

        var command = new CreateUploadSessionCommand(
            tenantId,
            farmId,
            missionId,
            request.OperationId,
            request.FileName,
            request.MimeType,
            request.MediaType,
            request.FileSizeBytes,
            request.Sha256Checksum,
            request.ExpectedMissionVersion);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new CreateMissionMediaUploadSessionResponse(
                    value.UploadSessionId,
                    value.MediaAssetId,
                    value.UploadUri.AbsoluteUri,
                    value.ExpiresAt,
                    value.Status.ToString(),
                    value.MissionVersion,
                    value.ReusedOperation)));
    }

    /// <summary>Xác nhận media đã được tải lên object storage.</summary>
    /// <remarks>
    /// Gọi API sau khi client PUT file thành công vào presigned URL. Backend
    /// kiểm tra object trên MinIO, gồm kích thước, MIME type và SHA-256 checksum,
    /// rồi chuyển upload session sang Completed. API có thể gọi lại an toàn khi
    /// kết quả hoàn tất trước đó vẫn hợp lệ.
    /// </remarks>
    [HttpPost(
    "api/farms/{farmId:guid}/missions/" +
    "{missionId:guid}/media/upload-sessions/" +
    "{uploadSessionId:guid}/complete")]
    public async Task<IResult> CompleteUploadSession(
    Guid farmId,
    Guid missionId,
    Guid uploadSessionId,
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

        var command = new CompleteUploadSessionCommand(
            tenantId,
            farmId,
            missionId,
            uploadSessionId);

        var result = await sender.Send(
            command,
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            value => Results.Ok(
                new CompleteMissionMediaUploadSessionResponse(
                    value.UploadSessionId,
                    value.MediaAssetId,
                    value.Status.ToString(),
                    value.MimeType,
                    value.FileSizeBytes,
                    value.Sha256Checksum,
                    value.ReusedCompletion)));
    }

}
