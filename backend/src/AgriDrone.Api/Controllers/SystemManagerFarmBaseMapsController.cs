using AgriDrone.Api.Contracts.Farms;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDraft;
using AgriDrone.Modules.Farms.Application.Features.Maps.PublishFarmBaseMap;
using AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidate;
using AgriDrone.Modules.Farms.Application.Features.Maps.ReviewMappingCandidatesBatch;
using AgriDrone.Modules.Farms.Application.Features.Maps.ValidateFarmBaseMapDraft;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/farms/{farmId:guid}/base-map-drafts")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerFarmBaseMapsController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Lấy chi tiết bản đồ khảo sát nháp và danh sách các candidate poles cần review.
    /// </summary>
    [HttpGet("{draftId:guid}")]
    [ProducesResponseType<FarmBaseMapDraftResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetDraft(
        [FromRoute] Guid farmId,
        [FromRoute] Guid draftId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetFarmBaseMapDraftQuery(farmId, draftId),
            cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// Review và ra quyết định đối với từng candidate (Accept, Reject, Relocate, Replace).
    /// </summary>
    [HttpPost("{draftId:guid}/candidates/{candidateId:guid}/review")]
    [ProducesResponseType<CandidateReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> ReviewCandidate(
        [FromRoute] Guid farmId,
        [FromRoute] Guid draftId,
        [FromRoute] Guid candidateId,
        [FromBody] ReviewCandidateApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MappingCandidateReviewDecision>(request.Decision, ignoreCase: true, out var decision))
        {
            return Results.BadRequest($"Invalid review decision '{request.Decision}'. Allowed values: Accept, Reject, Relocate, Replace.");
        }

        Point? correctedPoint = null;
        if (request.CorrectedLongitude.HasValue && request.CorrectedLatitude.HasValue)
        {
            correctedPoint = new Point(request.CorrectedLongitude.Value, request.CorrectedLatitude.Value)
            {
                SRID = 4326
            };
        }

        var result = await sender.Send(
            new ReviewMappingCandidateCommand(
                FarmId: farmId,
                DraftId: draftId,
                CandidateId: candidateId,
                Decision: decision,
                CorrectedPosition: correctedPoint,
                CorrectedRowIndex: request.CorrectedRowIndex,
                CorrectedColumnIndex: request.CorrectedColumnIndex,
                CorrectedMatchedPlantId: request.CorrectedMatchedPlantId,
                Reason: request.Reason),
            cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// Review hàng loạt nhiều candidates trong một lượt.
    /// </summary>
    [HttpPost("{draftId:guid}/candidates/review-batch")]
    [ProducesResponseType<BatchReviewSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> ReviewBatch(
        [FromRoute] Guid farmId,
        [FromRoute] Guid draftId,
        [FromBody] ReviewCandidatesBatchApiRequest request,
        CancellationToken cancellationToken)
    {
        var reviewItems = new List<CandidateBatchReviewItemDto>();

        foreach (var item in request.Reviews)
        {
            if (!Enum.TryParse<MappingCandidateReviewDecision>(item.Decision, ignoreCase: true, out var decision))
            {
                return Results.BadRequest($"Invalid review decision '{item.Decision}' for candidate {item.CandidateId}.");
            }

            Point? correctedPoint = null;
            if (item.CorrectedLongitude.HasValue && item.CorrectedLatitude.HasValue)
            {
                correctedPoint = new Point(item.CorrectedLongitude.Value, item.CorrectedLatitude.Value)
                {
                    SRID = 4326
                };
            }

            reviewItems.Add(new CandidateBatchReviewItemDto(
                CandidateId: item.CandidateId,
                Decision: decision,
                CorrectedPosition: correctedPoint,
                CorrectedRowIndex: item.CorrectedRowIndex,
                CorrectedColumnIndex: item.CorrectedColumnIndex,
                CorrectedMatchedPlantId: item.CorrectedMatchedPlantId,
                Reason: item.Reason));
        }

        var result = await sender.Send(
            new ReviewMappingCandidatesBatchCommand(
                FarmId: farmId,
                DraftId: draftId,
                Reviews: reviewItems,
                BatchReason: request.BatchReason),
            cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// Chạy authoritative validator kiểm tra toàn bộ batch trước khi phát hành bản đồ chính thức.
    /// </summary>
    [HttpPost("{draftId:guid}/validate")]
    [ProducesResponseType<BaselineMappingBatchValidationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> ValidateDraft(
        [FromRoute] Guid farmId,
        [FromRoute] Guid draftId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ValidateFarmBaseMapDraftQuery(farmId, draftId),
            cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>
    /// Phát hành bản đồ Farm Base Map chính thức, cấp phát Persistent Plant Identity và chuyển SurveyOrder sang AwaitingPricing.
    /// </summary>
    [HttpPost("{draftId:guid}/publish")]
    [ProducesResponseType<FarmBaseMapPublishedResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> PublishDraft(
        [FromRoute] Guid farmId,
        [FromRoute] Guid draftId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new PublishFarmBaseMapCommand(farmId, draftId),
            cancellationToken);

        return result.ToHttpResult(HttpContext, Results.Ok);
    }
}
