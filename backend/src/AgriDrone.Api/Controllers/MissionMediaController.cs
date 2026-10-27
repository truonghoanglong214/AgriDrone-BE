using AgriDrone.Api.Contracts.Missions;
using AgriDrone.Modules.Missions.Application.Features.Media.UploadMissionMedia;
using AgriDrone.Modules.Missions.Application.Features.Media;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMedia;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDetails;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMissionMediaDownloadUrl;
using AgriDrone.Modules.Missions.Application.Features.Media.CreateUploadSession;
using AgriDrone.Modules.Missions.Application.Features.Media.CompleteUploadSession;
using AgriDrone.Modules.Missions.Application.Features.Media.StartMultipartUpload;
using AgriDrone.Modules.Missions.Application.Features.Media.GetMultipartPartUrl;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Pagination;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/missions/{missionId:guid}/farms/{farmId:guid}/media")]
public sealed class MissionMediaController(
    ISender sender,
    ISystemManagerAccessService managerAccessService,
    ILogger<MissionMediaController> logger) : ControllerBase
{
    private const long MaximumBatchFileSizeBytes =
        5L * 1024 * 1024 * 1024;
    private const long MultipartRequestOverheadBytes = 1024L * 1024;
    private static readonly Action<ILogger, Guid, int, Exception?> LogTransferFailure =
        LoggerMessage.Define<Guid, int>(LogLevel.Error, new EventId(1, "MediaTransferFailed"),
            "Media upload interrupted for mission {MissionId}, file {Index}");

    /// <summary>Create or renew a direct-to-storage upload session for an assigned manager.</summary>
    /// <remarks>Tạo hoặc lấy lại phiên upload trực tiếp lên storage bằng Idempotency-Key header. Giữ nguyên key khi retry cùng file/ý định upload. Client dùng UploadUri được trả về để gửi file, sau đó gọi complete; ExpectedMissionVersion kiểm soát thay đổi đồng thời.</remarks>
    [HttpPost("upload-sessions")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> CreateUploadSession(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] CreateMissionMediaUploadSessionRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty UUID idempotency key is required."]
            });

        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
            return Results.Forbid();

        var result = await sender.Send(new CreateUploadSessionCommand(
            tenantId, farmId, missionId, idempotencyKey, request.FileName,
            request.MimeType, request.MediaType, request.FileSizeBytes,
            request.Sha256Checksum, request.ExpectedMissionVersion,
            null), cancellationToken);
        return result.ToHttpResult(HttpContext, value => Results.Ok(
            new CreateMissionMediaUploadSessionResponse(
                value.UploadSessionId, value.MediaAssetId, value.UploadUri.AbsoluteUri,
                value.ExpiresAt, value.Status.ToString(), value.MissionVersion,
                value.ReusedOperation)));
    }

    /// <summary>Create or resume a direct-to-storage multipart upload.</summary>
    /// <remarks>Tạo hoặc tiếp tục phiên multipart cho file lớn. Response cho biết kích thước part và các part đã upload để client tiếp tục sau khi mất kết nối.</remarks>
    [HttpPost("multipart-upload-sessions")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> StartMultipartUpload(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromBody] CreateMissionMediaUploadSessionRequest request,
        [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey == Guid.Empty)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Idempotency-Key"] = ["A non-empty UUID idempotency key is required."]
            });

        var access = await managerAccessService.ResolveFarmAccessAsync(farmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();

        var created = await sender.Send(new CreateUploadSessionCommand(
            tenantId, farmId, missionId, idempotencyKey, request.FileName,
            request.MimeType, request.MediaType, request.FileSizeBytes,
            request.Sha256Checksum, request.ExpectedMissionVersion,
            null),
            cancellationToken);
        if (created.IsFailure)
            return created.ToHttpResult(HttpContext, Results.Ok);

        var started = await sender.Send(new StartMultipartUploadCommand(
            tenantId, farmId, missionId, created.Value.UploadSessionId),
            cancellationToken);
        return started.ToHttpResult(HttpContext, value => Results.Ok(
            new StartMissionMultipartUploadResponse(
                value.UploadSessionId, created.Value.MediaAssetId,
                value.PartSizeBytes, value.PartCount, value.ExpiresAt,
                value.UploadedParts.Select(part =>
                    new UploadedPartResponse(part.Number, part.SizeBytes)).ToArray(),
                created.Value.ReusedOperation, value.ReusedUpload)));
    }

    /// <summary>Issue a short-lived URL for one multipart part.</summary>
    /// <remarks>Cấp URL có thời hạn cho một part của upload session thuộc đúng mission. Client gửi dữ liệu part trực tiếp tới storage.</remarks>
    [HttpGet("upload-sessions/{uploadSessionId:guid}/parts/{partNumber:int}/url")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IResult> GetMultipartPartUrl(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromRoute] Guid uploadSessionId, [FromRoute] int partNumber,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();
        var result = await sender.Send(new GetMultipartPartUrlQuery(
            tenantId, farmId, missionId, uploadSessionId, partNumber),
            cancellationToken);
        return result.ToHttpResult(HttpContext, value => Results.Ok(
            new MissionMultipartPartUrlResponse(value.PartNumber,
                value.SizeBytes, value.UploadUri.AbsoluteUri, value.ExpiresAt)));
    }

    /// <summary>Verify a directly uploaded object and attach it to the mission.</summary>
    /// <remarks>Kiểm tra object đã upload và hoàn tất media asset cho mission. Thời gian ảnh được xử lý ở bước finalize. Gọi lại phiên đã hoàn tất trả kết quả dùng lại thay vì tạo media trùng.</remarks>
    [HttpPost("upload-sessions/{uploadSessionId:guid}/complete")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    public async Task<IResult> CompleteUploadSession(
        [FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromRoute] Guid uploadSessionId,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId || access.FarmId != farmId)
            return Results.Forbid();

        var result = await sender.Send(new CompleteUploadSessionCommand(
            tenantId, farmId, missionId, uploadSessionId, null),
            cancellationToken);
        return result.ToHttpResult(HttpContext, value => Results.Ok(
            new CompleteMissionMediaUploadSessionResponse(
                value.UploadSessionId, value.MediaAssetId, value.Status.ToString(),
                value.MimeType, value.FileSizeBytes, value.Sha256Checksum,
                value.ReusedCompletion)));
    }

    /// <summary>Upload một hoặc nhiều ảnh/video trực tiếp cho Mission.</summary>
    /// <remarks>
    /// Gửi nhiều file trong trường files, tổng dữ liệu file tối đa 5 GiB.
    /// Backend tính SHA-256; file lớn hơn 50 MiB được upload multipart nội bộ,
    /// file nhỏ được upload trực tiếp. FE không cần gọi API upload session riêng.
    /// Backend hoàn tất media sau khi xác minh file trên storage.
    /// Mission phải đã kết thúc chuyến bay. Gửi lại cùng nội dung trong cùng
    /// Mission trả media đã hoàn tất.
    /// Telemetry và finalize upload được thực hiện qua API riêng.
    /// Xử lý tuần tự; khi một file lỗi, dừng và đánh dấu các file còn lại NotAttempted.
    /// File đã Completed được giữ lại. HTTP 200 nếu tất cả thành công, 207 nếu
    /// có lỗi. Response chứa kết quả theo index (bắt đầu từ 0) và tên từng file.
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumBatchFileSizeBytes + MultipartRequestOverheadBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaximumBatchFileSizeBytes +
                                   MultipartRequestOverheadBytes)]
    [ProducesResponseType(typeof(IReadOnlyList<UploadMissionMediaItemResponse>), 200)]
    [ProducesResponseType(typeof(IReadOnlyList<UploadMissionMediaItemResponse>), 207)]
    public async Task<IResult> Upload([FromRoute] Guid farmId, [FromRoute] Guid missionId,
        [FromForm] UploadMissionMediaRequest request, CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            farmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != farmId)
            return Results.Forbid();

        if (request.Files.Sum(file => file.Length) > MaximumBatchFileSizeBytes)
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: "Total file size must not exceed 5 GiB per request.");

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
                    tenantId, farmId, missionId, content, null, fileName),
                    cancellationToken);
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

    /// <summary>Lấy danh sách media đã hoàn tất của Mission.</summary>
    /// <remarks>
    /// Yêu cầu SystemManager đang được phân công Farm. Phân trang tối đa 100 phần tử, lọc MediaType/MediaRole.
    /// Chỉ trả media Active thuộc đúng tenant, farm và mission; không trả storage URI.
    /// Mission tồn tại chưa có media trả trang rỗng; mission không tồn tại trả 404.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ProducesResponseType(typeof(PagedResult<MissionMediaResponse>), StatusCodes.Status200OK)]
    public async Task<IResult> GetMedia(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromQuery] GetMissionMediaRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeFarmAsync(farmId);
        if (denied is not null) return denied;

        var result = await sender.Send(new GetMissionMediaQuery(
            farmId, missionId, request.PageNumber, request.PageSize,
            request.MediaType, request.MediaRole), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>Lấy chi tiết media của Mission.</summary>
    /// <remarks>
    /// Yêu cầu SystemManager đang được phân công Farm. Media phải Active và liên kết với đúng mission/farm/tenant.
    /// Trả metadata, checksum và thông tin capture; không trả storage URI nội bộ.
    /// </remarks>
    [HttpGet("{mediaId:guid}")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ProducesResponseType(typeof(MissionMediaResponse), StatusCodes.Status200OK)]
    public async Task<IResult> GetMediaDetails(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromRoute] Guid mediaId,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeFarmAsync(farmId);
        if (denied is not null) return denied;

        var result = await sender.Send(new GetMissionMediaDetailsQuery(
            farmId, missionId, mediaId), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>Cấp link xem/tải media có hiệu lực 5 phút.</summary>
    /// <remarks>
    /// Kiểm tra quyền SystemManager trên Farm, liên kết media và object trước khi cấp presigned GET URL.
    /// Không cache response. Link đã cấp còn dùng được tới khi hết hạn.
    /// </remarks>
    [HttpGet("{mediaId:guid}/download-url")]
    [Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
    [ProducesResponseType(typeof(MissionMediaDownloadResponse), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IResult> GetDownloadUrl(
        [FromRoute] Guid farmId,
        [FromRoute] Guid missionId,
        [FromRoute] Guid mediaId,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeFarmAsync(farmId);
        if (denied is not null) return denied;

        var result = await sender.Send(new GetMissionMediaDownloadUrlQuery(
            farmId, missionId, mediaId), cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    private async Task<IResult?> AuthorizeFarmAsync(Guid farmId)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(farmId, HttpContext.RequestAborted);
        return access.IsAllowed && access.FarmId == farmId ? null : Results.Forbid();
    }
}
