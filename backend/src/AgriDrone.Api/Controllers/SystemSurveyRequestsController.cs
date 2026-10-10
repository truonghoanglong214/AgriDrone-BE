using AgriDrone.Api.Contracts.Surveys.Requests;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Requests.Common;
using AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestDetail;
using AgriDrone.Modules.Surveys.Application.Features.Requests.GetSurveyRequestInbox;
using AgriDrone.Modules.Surveys.Application.Features.Requests.RejectSurveyRequest;
using AgriDrone.Modules.Surveys.Application.Features.Requests.StartSurveyRequestReview;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-requests")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyRequestsController(ISender sender)
    : ControllerBase
{
    /// <summary>Lấy inbox yêu cầu khảo sát để review.</summary>
    /// <remarks>
    /// Hỗ trợ filter status, kind, service và khoảng CreatedAt UTC. Kết quả được
    /// sắp xếp ổn định theo CreatedAt giảm dần rồi Id giảm dần.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<SurveyRequestInboxItemApiResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IResult> GetInbox(
        [FromQuery] GetSurveyRequestsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyRequestInboxQuery(
                MapStatus(request.Status),
                MapKind(request.Kind),
                request.SurveyServiceId,
                request.CreatedFrom,
                request.CreatedTo,
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            page => Results.Ok(
                SurveyRequestResponseMapper.ToResponse(page)));
    }

    /// <summary>Lấy snapshot và review history của một yêu cầu.</summary>
    /// <remarks>
    /// CandidateLocation chỉ là dữ liệu intake, không phải approved FarmBoundary.
    /// Response kèm checklist version hiện hành để client gửi quyết định review.
    /// </remarks>
    [HttpGet("{requestId:guid}")]
    [ProducesResponseType<SurveyRequestDetailApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetDetail(
        [FromRoute] Guid requestId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyRequestDetailQuery(requestId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyRequestResponseMapper.ToResponse(response)));
    }

    /// <summary>Bắt đầu review một yêu cầu đang Submitted.</summary>
    [HttpPost("{requestId:guid}/start-review")]
    [ProducesResponseType<SurveyRequestReviewMutationApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> StartReview(
        [FromRoute] Guid requestId,
        [FromBody] StartSurveyRequestReviewRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new StartSurveyRequestReviewCommand(
                requestId,
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyRequestResponseMapper.ToResponse(response)));
    }

    /// <summary>Từ chối một yêu cầu đang UnderReview.</summary>
    /// <remarks>
    /// Reason và checklist snapshot được lưu append-only. Transition, audit và
    /// rejection notification outbox được commit trong cùng transaction.
    /// </remarks>
    [HttpPost("{requestId:guid}/reject")]
    [ProducesResponseType<SurveyRequestRejectionApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> Reject(
        [FromRoute] Guid requestId,
        [FromBody] RejectSurveyRequest request,
        CancellationToken cancellationToken)
    {
        var checklist = request.Checklist;
        var result = await sender.Send(
            new RejectSurveyRequestCommand(
                requestId,
                request.Reason,
                new SurveyRequestReviewChecklistInput(
                    checklist.Version,
                    checklist.ContactValid,
                    checklist.IsDragonFruitFarm,
                    checklist.IsServiceAreaSupported,
                    checklist.IsLocationSufficient,
                    checklist.IsPreliminaryLegalAndFlightFeasible,
                    checklist.IsEligibleSystemManagerAvailable,
                    checklist.Notes),
                request.ExpectedVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyRequestResponseMapper.ToResponse(response)));
    }

    private static SurveyRequestStatus? MapStatus(
        SurveyRequestStatusFilterValue? status) =>
        status switch
        {
            null => null,
            SurveyRequestStatusFilterValue.SUBMITTED =>
                SurveyRequestStatus.Submitted,
            SurveyRequestStatusFilterValue.UNDER_REVIEW =>
                SurveyRequestStatus.UnderReview,
            SurveyRequestStatusFilterValue.APPROVED =>
                SurveyRequestStatus.Approved,
            SurveyRequestStatusFilterValue.REJECTED =>
                SurveyRequestStatus.Rejected,
            SurveyRequestStatusFilterValue.WITHDRAWN =>
                SurveyRequestStatus.Withdrawn,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported survey request status filter.")
        };

    private static SurveyRequestKind? MapKind(
        SurveyRequestKindFilterValue? kind) =>
        kind switch
        {
            null => null,
            SurveyRequestKindFilterValue.NEW_CUSTOMER =>
                SurveyRequestKind.NewCustomer,
            SurveyRequestKindFilterValue.EXISTING_TENANT_NEW_FARM =>
                SurveyRequestKind.ExistingTenantNewFarm,
            SurveyRequestKindFilterValue.EXISTING_FARM_SURVEY =>
                SurveyRequestKind.ExistingFarmSurvey,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported survey request kind filter.")
        };
}
