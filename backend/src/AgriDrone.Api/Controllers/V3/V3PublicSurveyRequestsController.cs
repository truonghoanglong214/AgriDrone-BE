using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequestV3;
using AgriDrone.Modules.Surveys.Application.Features.Requests.TrackSurveyRequestV3;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/public/survey-requests")]
[AllowAnonymous]
public sealed class V3PublicSurveyRequestsController(
    ISender sender,
    IPublicSurveyRequestCallerScope callerScope) : ControllerBase
{
    /// <summary>Gửi yêu cầu khảo sát V3 cho khách hàng mới chưa có tài khoản.</summary>
    /// <remarks>
    /// Yêu cầu Idempotency-Key. Cổng tiếp nhận công khai phụ thuộc cờ PublicIntakeEnabled.
    /// Trả về quote tham khảo và mã theo dõi trk_... để tra cứu tiến độ.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<SubmitNewCustomerSurveyRequestV3Response>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Submit(
        [FromHeader(Name = SurveyRequestHttpIdempotency.HeaderName), Required]
        [StringLength(SurveyRequestHttpIdempotency.MaximumKeyLength, MinimumLength = 1)]
        string idempotencyKey,
        [FromBody] SubmitNewCustomerSurveyRequestV3ApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!SurveyRequestHttpIdempotency.TryNormalize(
                HttpContext,
                idempotencyKey,
                out var normalizedKey,
                out var error))
        {
            return error!;
        }

        var idempotency = RequestIdempotency.Create(
            callerScope.GetOrCreate(HttpContext),
            normalizedKey);

        var result = await sender.Send(
            new SubmitNewCustomerSurveyRequestV3Command(
                idempotency,
                request.SurveyServiceId,
                request.TenantCode,
                request.TenantName,
                request.ApplicantName,
                request.ApplicantEmail,
                request.ApplicantPhone,
                request.FarmName,
                request.FarmAddress,
                request.ApproximateAreaHa,
                request.Longitude,
                request.Latitude,
                request.EstimatedPoleCount,
                request.PreferredStartAt,
                request.PreferredEndAt,
                request.Notes),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Tra cứu trạng thái yêu cầu khảo sát V3 bằng mã tracking.</summary>
    /// <remarks>
    /// Mã theo dõi được gửi trong body (không đưa lên URL hoặc log) và được hash SHA-256 để tra cứu.
    /// Response chỉ trả về thông tin tối thiểu, tuyệt đối không lộ thông tin cá nhân (PII).
    /// </remarks>
    [HttpPost("track")]
    [ProducesResponseType<TrackSurveyRequestV3Response>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Track(
        [FromBody] TrackSurveyRequestV3ApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new TrackSurveyRequestV3Query(request.TrackingToken),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(response));
    }
}
