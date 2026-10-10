using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Api.Mapping;
using AgriDrone.Api.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitExistingFarmSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Requests.SubmitNewFarmSurveyRequest;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/tenant-owner")]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class TenantOwnerSurveyRequestsController(ISender sender)
    : ControllerBase
{
    /// <summary>Gửi yêu cầu khảo sát để đề nghị tạo Farm mới.</summary>
    /// <remarks>
    /// TenantId, UserId và applicant contact được lấy từ authenticated execution
    /// context và Identity profile, không lấy từ body.
    /// </remarks>
    [HttpPost("survey-requests/new-farm")]
    [ProducesResponseType<SurveyRequestAcknowledgementApiResponse>(
        StatusCodes.Status201Created)]
    public async Task<IResult> SubmitNewFarm(
        [FromHeader(Name = SurveyRequestHttpIdempotency.HeaderName), Required]
        [StringLength(
            SurveyRequestHttpIdempotency.MaximumKeyLength,
            MinimumLength = 1)]
        string idempotencyKey,
        [FromBody] SubmitNewFarmSurveyRequest request,
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
            new SubmitNewFarmSurveyRequestCommand(
                normalizedKey,
                request.SurveyServiceId,
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

    /// <summary>Gửi yêu cầu khảo sát cho Farm hiện hữu.</summary>
    /// <remarks>
    /// FarmId lấy từ route. Farm-to-Tenant ownership, Farm status và Farm snapshot
    /// được resolve phía server; body không thể chọn TenantId hoặc FarmId khác.
    /// </remarks>
    [HttpPost("farms/{farmId:guid}/survey-requests")]
    [ProducesResponseType<SurveyRequestAcknowledgementApiResponse>(
        StatusCodes.Status201Created)]
    public async Task<IResult> SubmitExistingFarm(
        [FromRoute] Guid farmId,
        [FromHeader(Name = SurveyRequestHttpIdempotency.HeaderName), Required]
        [StringLength(
            SurveyRequestHttpIdempotency.MaximumKeyLength,
            MinimumLength = 1)]
        string idempotencyKey,
        [FromBody] SubmitExistingFarmSurveyRequest request,
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
            new SubmitExistingFarmSurveyRequestCommand(
                farmId,
                normalizedKey,
                request.SurveyServiceId,
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
