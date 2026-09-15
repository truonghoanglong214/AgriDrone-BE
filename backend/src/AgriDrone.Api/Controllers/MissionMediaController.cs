using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
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
    IExecutionContext executionContext,
    ILogger<MissionMediaController> logger) : ControllerBase
{
    private static readonly Action<ILogger, Guid, int, Exception?> LogTransferFailure =
        LoggerMessage.Define<Guid, int>(LogLevel.Error, new EventId(1, "MediaTransferFailed"),
            "Media upload interrupted for mission {MissionId}, file {Index}");

    /// <summary>Upload một hoặc nhiều ảnh/video trực tiếp cho Mission.</summary>
    /// <remarks>
    /// Gửi 1–20 file trong trường files. JPEG/PNG tối đa 50 MiB mỗi file;
    /// MP4/MOV tối đa 5 GiB. Tổng dữ liệu file tối đa 5 GiB mỗi request.
    /// Backend tính SHA-256, upload MinIO và hoàn tất media.
    /// Không cần gửi metadata, operationId hay version. Mission phải đã kết thúc
    /// chuyến bay. Gửi lại cùng nội dung trong cùng Mission trả media đã hoàn tất.
    /// Telemetry và finalize upload được thực hiện qua API riêng.
    /// Xử lý tuần tự; khi một file lỗi, dừng và đánh dấu các file còn lại NotAttempted.
    /// File đã Completed được giữ lại. HTTP 200 nếu tất cả thành công, 207 nếu
    /// có lỗi. Response chứa kết quả theo index (bắt đầu từ 0) và tên từng file.
    /// </remarks>
    [HttpPost("api/missions/{missionId:guid}/media")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5L * 1024 * 1024 * 1024 + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5L * 1024 * 1024 * 1024)]
    [ProducesResponseType(typeof(IReadOnlyList<UploadMissionMediaItemResponse>), 200)]
    [ProducesResponseType(typeof(IReadOnlyList<UploadMissionMediaItemResponse>), 207)]
    public async Task<IResult> Upload([FromQuery] Guid farmId, [FromRoute] Guid missionId,
        [FromForm] UploadMissionMediaRequest request, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
            return Results.Unauthorized();
        var authorization = await authorizationService.AuthorizeAsync(User,
            new FarmAccessTarget(tenantId, farmId), AccessAuthorizationPolicies.FarmManage);
        if (!authorization.Succeeded)
            return Results.Forbid();

        if (request.Files.Sum(file => file.Length) > 5L * 1024 * 1024 * 1024)
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: "Total file size must not exceed 5 GiB.");

        var results = new List<UploadMissionMediaItemResponse>();
        var stopped = false;
        for (var index = 0; index < request.Files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = request.Files[index];
            var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (stopped)
            {
                results.Add(new(index, fileName, "NotAttempted", null, null, null));
                continue;
            }

            try
            {
                await using var content = file.OpenReadStream();
                var result = await sender.Send(new UploadMissionMediaCommand(
                    tenantId, farmId, missionId, content), cancellationToken);
                if (result.IsFailure)
                {
                    results.Add(new(index, fileName, "Failed", null,
                        result.Error.Code, result.Error.Description));
                    stopped = true;
                    continue;
                }

                var value = result.Value;
                results.Add(new(index, fileName, "Completed",
                    new CompleteMissionMediaUploadSessionResponse(value.UploadSessionId,
                        value.MediaAssetId, value.Status.ToString(), value.MimeType,
                        value.FileSizeBytes, value.Sha256Checksum, value.ReusedCompletion),
                    null, null));
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                // Do not continue with the scoped unit of work after an interrupted transfer.
                LogTransferFailure(logger, missionId, index, exception);
                results.Add(new(index, fileName, "Failed", null,
                    "MediaUpload.TransferFailed", "File transfer failed. Retry this file."));
                stopped = true;
            }
        }

        return Results.Json(results, statusCode: stopped ? 207 : StatusCodes.Status200OK);
    }
}
