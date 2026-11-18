using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitTenantOwnerSurveyRequestV3;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/tenant-owner/survey-requests")]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class V3TenantOwnerSurveyRequestsController(ISender sender) : ControllerBase
{
    /// <summary>Gửi yêu cầu khảo sát V3 cho chủ nông trại (Farm mới hoặc khảo sát định kỳ Farm hiện hữu).</summary>
    /// <remarks>
    /// Đối với khảo sát định kỳ (có FarmId), bắt buộc Farm đã có Base Map hợp lệ.
    /// Yêu cầu Idempotency-Key để chống gửi trùng lặp.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<SubmitTenantOwnerSurveyRequestV3Response>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Submit(
        [FromHeader(Name = SurveyRequestHttpIdempotency.HeaderName), Required]
        [StringLength(SurveyRequestHttpIdempotency.MaximumKeyLength, MinimumLength = 1)]
        string idempotencyKey,
        [FromBody] SubmitTenantOwnerSurveyRequestV3ApiRequest request,
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

        var result = await sender.Send(
            new SubmitTenantOwnerSurveyRequestV3Command(
                normalizedKey,
                request.SurveyServiceId,
                request.FarmId,
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
}
