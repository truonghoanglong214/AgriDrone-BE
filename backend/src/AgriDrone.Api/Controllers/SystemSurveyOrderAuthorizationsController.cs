using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Integrations.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizationDetail;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.GetSurveyCaptureAuthorizations;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.RevokeSurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Application.Features.Authorizations.VerifySurveyCaptureAuthorization;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-orders/{orderId:guid}/capture-authorizations")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyOrderAuthorizationsController(
    ISender sender,
    IObjectStorageWriter storageWriter,
    IObjectStorage storageReader,
    IOptions<MinioStorageOptions> storageOptions)
    : ControllerBase
{
    /// <summary>Lấy danh sách tất cả các ủy quyền thu thập dữ liệu (Consent, Flight Permission) của đơn khảo sát.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SurveyCaptureAuthorizationApiResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAll(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSurveyCaptureAuthorizationsQuery(orderId), cancellationToken);
        return result.ToHttpResult(HttpContext, items => Results.Ok(items.Select(Map)));
    }

    /// <summary>Lấy chi tiết một ủy quyền thu thập dữ liệu theo loại (consent, flight-permission).</summary>
    [HttpGet("{type}")]
    [ProducesResponseType<SurveyCaptureAuthorizationApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetByType(
        Guid orderId,
        string type,
        CancellationToken cancellationToken)
    {
        if (!TryParseType(type, out var authorizationType)) return Results.BadRequest();
        var result = await sender.Send(
            new GetSurveyCaptureAuthorizationDetailQuery(orderId, authorizationType),
            cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    /// <summary>Xác thực văn bản đồng ý sử dụng dữ liệu nông trại (Consent) bằng file ảnh/PDF hoặc mã mock.</summary>
    [HttpPut("consent/verify")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<SurveyCaptureAuthorizationApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> VerifyConsent(
        Guid orderId,
        [FromForm] VerifySurveyCaptureAuthorizationForm form,
        CancellationToken cancellationToken)
    {
        var evidenceResult = await ResolveEvidenceReferenceAsync(
            orderId, "consent", form, cancellationToken);
        if (evidenceResult.IsFailure)
        {
            return Results.BadRequest(evidenceResult.Error.Description);
        }

        var result = await sender.Send(new VerifySurveyCaptureAuthorizationCommand(
            orderId, SurveyCaptureAuthorizationType.WrittenDataUseConsent, evidenceResult.Value, form.IssuedAt,
            form.ExpiresAt, form.ExpectedVersion), cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    /// <summary>Thu hồi văn bản đồng ý sử dụng dữ liệu nông trại (Consent).</summary>
    [HttpPut("consent/revoke")]
    [ProducesResponseType<SurveyCaptureAuthorizationApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RevokeConsent(
        Guid orderId,
        RevokeSurveyCaptureAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevokeSurveyCaptureAuthorizationCommand(
            orderId, SurveyCaptureAuthorizationType.WrittenDataUseConsent, request.Reason, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    /// <summary>Lấy liên kết xem/tải bằng chứng (Presigned URL) của văn bản đồng ý sử dụng dữ liệu (Consent).</summary>
    [HttpGet("consent/evidence-url")]
    [ProducesResponseType<SurveyAuthorizationEvidenceUrlApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IResult> GetConsentEvidenceUrl(Guid orderId, CancellationToken cancellationToken) =>
        GetEvidenceUrlInternal(orderId, SurveyCaptureAuthorizationType.WrittenDataUseConsent, cancellationToken);

    /// <summary>Xác thực giấy phép bay từ cơ quan có thẩm quyền (Flight Permission) bằng file ảnh/PDF hoặc mã mock.</summary>
    [HttpPut("flight-permission/verify")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<SurveyCaptureAuthorizationApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> VerifyFlightPermission(
        Guid orderId,
        [FromForm] VerifySurveyCaptureAuthorizationForm form,
        CancellationToken cancellationToken)
    {
        var evidenceResult = await ResolveEvidenceReferenceAsync(
            orderId, "flight-permission", form, cancellationToken);
        if (evidenceResult.IsFailure)
        {
            return Results.BadRequest(evidenceResult.Error.Description);
        }

        var result = await sender.Send(new VerifySurveyCaptureAuthorizationCommand(
            orderId, SurveyCaptureAuthorizationType.FlightPermission, evidenceResult.Value, form.IssuedAt,
            form.ExpiresAt, form.ExpectedVersion), cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    /// <summary>Thu hồi giấy phép bay (Flight Permission).</summary>
    [HttpPut("flight-permission/revoke")]
    [ProducesResponseType<SurveyCaptureAuthorizationApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RevokeFlightPermission(
        Guid orderId,
        RevokeSurveyCaptureAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RevokeSurveyCaptureAuthorizationCommand(
            orderId, SurveyCaptureAuthorizationType.FlightPermission, request.Reason, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    /// <summary>Lấy liên kết xem/tải bằng chứng (Presigned URL) của giấy phép bay (Flight Permission).</summary>
    [HttpGet("flight-permission/evidence-url")]
    [ProducesResponseType<SurveyAuthorizationEvidenceUrlApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IResult> GetFlightPermissionEvidenceUrl(Guid orderId, CancellationToken cancellationToken) =>
        GetEvidenceUrlInternal(orderId, SurveyCaptureAuthorizationType.FlightPermission, cancellationToken);

    [HttpPut("{type}/verify")]
    public async Task<IResult> Verify(
        Guid orderId,
        string type,
        VerifySurveyCaptureAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseType(type, out var authorizationType)) return Results.BadRequest();
        var result = await sender.Send(new VerifySurveyCaptureAuthorizationCommand(
            orderId, authorizationType, request.EvidenceReference, request.IssuedAt,
            request.ExpiresAt, request.ExpectedVersion), cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    [HttpPut("{type}/revoke")]
    public async Task<IResult> Revoke(
        Guid orderId,
        string type,
        RevokeSurveyCaptureAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseType(type, out var authorizationType)) return Results.BadRequest();
        var result = await sender.Send(new RevokeSurveyCaptureAuthorizationCommand(
            orderId, authorizationType, request.Reason, request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(HttpContext, item => Results.Ok(Map(item)));
    }

    /// <summary>Lấy liên kết xem/tải bằng chứng (Presigned URL) theo loại authorization.</summary>
    [HttpGet("{type}/evidence-url")]
    [ProducesResponseType<SurveyAuthorizationEvidenceUrlApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetEvidenceUrl(Guid orderId, string type, CancellationToken cancellationToken)
    {
        if (!TryParseType(type, out var authorizationType)) return Results.BadRequest();
        return await GetEvidenceUrlInternal(orderId, authorizationType, cancellationToken);
    }

    private async Task<IResult> GetEvidenceUrlInternal(
        Guid orderId,
        SurveyCaptureAuthorizationType type,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyCaptureAuthorizationDetailQuery(orderId, type), cancellationToken);
        if (result.IsFailure)
        {
            return result.ToHttpResult(HttpContext, _ => Results.NotFound());
        }

        var auth = result.Value;
        if (string.IsNullOrWhiteSpace(auth.EvidenceReference))
        {
            return Results.NotFound(new { Message = "Chưa có bằng chứng nào được tải lên cho ủy quyền này." });
        }

        var typeString = type == SurveyCaptureAuthorizationType.WrittenDataUseConsent
            ? "WRITTEN_DATA_USE_CONSENT" : "FLIGHT_PERMISSION";

        // Trường hợp 1: Dữ liệu Mock
        if (auth.EvidenceReference.StartsWith("mock:", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new SurveyAuthorizationEvidenceUrlApiResponse(
                orderId,
                typeString,
                auth.Status.ToString().ToUpperInvariant(),
                auth.EvidenceReference,
                IsMock: true,
                DownloadUrl: null,
                UrlExpiresAt: null,
                Note: "Bằng chứng giả lập (Mock mode cho sinh viên)."));
        }

        // Trường hợp 2: Bằng chứng lưu trên MinIO (minio://...)
        if (auth.EvidenceReference.StartsWith("minio://", StringComparison.OrdinalIgnoreCase))
        {
            if (!storageOptions.Value.Enabled)
            {
                return Results.Ok(new SurveyAuthorizationEvidenceUrlApiResponse(
                    orderId,
                    typeString,
                    auth.Status.ToString().ToUpperInvariant(),
                    auth.EvidenceReference,
                    IsMock: false,
                    DownloadUrl: null,
                    UrlExpiresAt: null,
                    Note: "Dịch vụ lưu trữ MinIO đang tắt trong cấu hình hệ thống."));
            }

            try
            {
                var lifetime = TimeSpan.FromMinutes(storageOptions.Value.PresignedUrlExpiryMinutes > 0
                    ? storageOptions.Value.PresignedUrlExpiryMinutes
                    : 30);

                var downloadUri = await storageReader.CreateDownloadUriAsync(
                    auth.EvidenceReference, lifetime, cancellationToken);

                return Results.Ok(new SurveyAuthorizationEvidenceUrlApiResponse(
                    orderId,
                    typeString,
                    auth.Status.ToString().ToUpperInvariant(),
                    auth.EvidenceReference,
                    IsMock: false,
                    DownloadUrl: downloadUri.ToString(),
                    UrlExpiresAt: DateTimeOffset.UtcNow.Add(lifetime),
                    Note: "Liên kết tải ảnh/tài liệu từ MinIO (có hiệu lực tạm thời)."));
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: $"Không thể tạo liên kết tải từ MinIO: {ex.Message}",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        // Trường hợp 3: Bằng chứng chuỗi khác
        return Results.Ok(new SurveyAuthorizationEvidenceUrlApiResponse(
            orderId,
            typeString,
            auth.Status.ToString().ToUpperInvariant(),
            auth.EvidenceReference,
            IsMock: false,
            DownloadUrl: null,
            UrlExpiresAt: null,
            Note: "Bằng chứng tham chiếu dạng chuỗi tùy chỉnh."));
    }

    private async Task<Result<string>> ResolveEvidenceReferenceAsync(
        Guid orderId,
        string authorizationType,
        VerifySurveyCaptureAuthorizationForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is not null && form.File.Length > 0)
        {
            if (!storageOptions.Value.Enabled)
            {
                return Result.Failure<string>(
                    AppError.Validation(
                        "Storage.Disabled",
                        "Dịch vụ lưu trữ MinIO đang tắt. Vui lòng bật ObjectStorage trong cấu hình hoặc sử dụng mã mock khi demo."));
            }

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB
            if (form.File.Length > maxFileSize)
            {
                return Result.Failure<string>(
                    AppError.Validation("File.TooLarge", "File bằng chứng không được vượt quá 10MB."));
            }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".pdf"
            };
            var extension = Path.GetExtension(form.File.FileName);
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                return Result.Failure<string>(
                    AppError.Validation("File.InvalidFormat", "Chỉ chấp nhận file định dạng .jpg, .jpeg, .png hoặc .pdf."));
            }

            var bucket = string.IsNullOrWhiteSpace(storageOptions.Value.Bucket)
                ? "agridrone-media"
                : storageOptions.Value.Bucket;

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var objectName = $"evidences/orders/{orderId:D}/{authorizationType.ToLowerInvariant()}_{timestamp}{extension.ToLowerInvariant()}";
            var storageUri = $"minio://{bucket}/{objectName}";

            var mimeType = string.IsNullOrWhiteSpace(form.File.ContentType)
                ? "application/octet-stream"
                : form.File.ContentType;

            await using var stream = form.File.OpenReadStream();
            await storageWriter.UploadAsync(storageUri, stream, form.File.Length, mimeType, cancellationToken);

            return Result.Success(storageUri);
        }

        if (!string.IsNullOrWhiteSpace(form.MockReference))
        {
            var mockRef = form.MockReference.Trim();
            var evidenceRef = mockRef.StartsWith("mock:", StringComparison.OrdinalIgnoreCase)
                ? mockRef
                : $"mock:{mockRef}";

            return Result.Success(evidenceRef);
        }

        return Result.Failure<string>(
            AppError.Validation("Evidence.Required", "Vui lòng đính kèm file bằng chứng (ảnh/PDF) hoặc cung cấp mã mock."));
    }

    private static bool TryParseType(
        string value,
        out SurveyCaptureAuthorizationType type)
    {
        type = value.Trim().ToUpperInvariant() switch
        {
            "WRITTEN_DATA_USE_CONSENT" or "CONSENT" or "DATA-USE-CONSENT" => SurveyCaptureAuthorizationType.WrittenDataUseConsent,
            "FLIGHT_PERMISSION" or "FLIGHT-PERMISSION" => SurveyCaptureAuthorizationType.FlightPermission,
            _ => (SurveyCaptureAuthorizationType)(-1)
        };
        return Enum.IsDefined(type);
    }

    private static SurveyCaptureAuthorizationApiResponse Map(
        SurveyCaptureAuthorizationResponse item) => new(
            item.Id, item.SurveyOrderId,
            item.Type == SurveyCaptureAuthorizationType.WrittenDataUseConsent
                ? "WRITTEN_DATA_USE_CONSENT" : "FLIGHT_PERMISSION",
            item.Status.ToString().ToUpperInvariant(),
            null, null,
            item.VerifiedAt,
            item.ExpiresAt, item.RevokedAt,
            null,
            item.Version);

    private static SurveyCaptureAuthorizationApiResponse Map(
        SurveyCaptureAuthorizationDetailResponse item) => new(
            item.Id, item.SurveyOrderId,
            item.Type == SurveyCaptureAuthorizationType.WrittenDataUseConsent
                ? "WRITTEN_DATA_USE_CONSENT" : "FLIGHT_PERMISSION",
            item.Status.ToString().ToUpperInvariant(),
            item.EvidenceReference,
            item.IssuedAt,
            item.VerifiedAt,
            item.ExpiresAt,
            item.RevokedAt,
            item.VerifiedBy,
            item.Version);
}
