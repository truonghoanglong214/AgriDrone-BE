using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Api.Mapping;
using AgriDrone.Api.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewCustomerSurveyRequest;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/public/survey-requests")]
[AllowAnonymous]
public sealed class PublicSurveyRequestsController(
    ISender sender,
    IPublicSurveyRequestCallerScope callerScope) : ControllerBase
{
    /// <summary>Gửi yêu cầu khảo sát cho khách hàng chưa có Tenant.</summary>
    /// <remarks>
    /// Idempotency-Key là bắt buộc. Server cấp cookie HttpOnly ẩn danh để cô lập
    /// idempotency giữa các public caller; Tenant/User/Farm chưa được provision.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<SurveyRequestAcknowledgementApiResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Submit(
        [FromHeader(Name = SurveyRequestHttpIdempotency.HeaderName), Required]
        [StringLength(
            SurveyRequestHttpIdempotency.MaximumKeyLength,
            MinimumLength = 1)]
        string idempotencyKey,
        [FromBody] SubmitNewCustomerSurveyRequest request,
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
            new SubmitNewCustomerSurveyRequestCommand(
                idempotency,
                request.SurveyServiceId,
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
            response => Results.Json(
                SurveyRequestResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }
}
