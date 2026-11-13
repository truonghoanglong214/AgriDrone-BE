using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.Approvals;
using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Api.Mapping;
using AgriDrone.Api.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/system-admin/survey-requests")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class V3SystemAdminSurveyRequestsController(ISender sender) : ControllerBase
{
    /// <summary>Phê duyệt đơn yêu cầu khảo sát V3 (phân bổ Order, Commercial Snapshot, State History).</summary>
    /// <remarks>
    /// Thực thi trong một physical transaction duy nhất. Đảm bảo snapshot giá/cọc/claim không bị trôi
    /// khi quản trị viên cập nhật cấu hình trong tương lai.
    /// </remarks>
    [HttpPost("{requestId:guid}/approve")]
    [ProducesResponseType<ApproveSurveyRequestApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Approve(
        [FromRoute] Guid requestId,
        [FromHeader(Name = SurveyRequestHttpIdempotency.HeaderName), Required]
        [StringLength(SurveyRequestHttpIdempotency.MaximumKeyLength, MinimumLength = 1)]
        string idempotencyKey,
        [FromBody, Required] ApproveSurveyRequest request,
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

        var checklist = request.Checklist;
        var result = await sender.Send(
            new ApproveSurveyRequestCommand(
                requestId,
                request.SelectedSystemManagerProfileId,
                request.Reason,
                MapChecklist(checklist),
                request.ExpectedVersion!.Value,
                normalizedKey),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyRequestResponseMapper.ToResponse(response)));
    }

    private static SurveyRequestReviewChecklistInput MapChecklist(
        SurveyRequestReviewChecklistRequest checklist) =>
        new(
            SurveyRequestReviewChecklistDefinition.Version,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                [SurveyRequestReviewChecklistDefinition.ContactValidCode] = checklist.ContactValid,
                [SurveyRequestReviewChecklistDefinition.DragonFruitFarmConfirmedCode] = checklist.DragonFruitFarmConfirmed,
                [SurveyRequestReviewChecklistDefinition.ServiceAreaSupportedCode] = checklist.ServiceAreaSupported,
                [SurveyRequestReviewChecklistDefinition.CandidateLocationSufficientCode] = checklist.CandidateLocationSufficient
            },
            checklist.Notes);
}
