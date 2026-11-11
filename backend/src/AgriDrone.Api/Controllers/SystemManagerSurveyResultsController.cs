using AgriDrone.Modules.Surveys.Application.Abstractions.HealthReview;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.ApprovePlantHealthSurveyResult;
using AgriDrone.Modules.Surveys.Application.Features.Results.PlantHealth.PublishPlantHealthSurveyResult;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/survey-orders/{orderId:guid}")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
[RequestSizeLimit(1_048_576)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
public sealed class SystemManagerSurveyResultsController(IPlantHealthReviewService reviews, IPlantHealthResultPublisher publisher,
    IPlantHealthPublishedResultReader published) : ControllerBase
{
    /// <summary>Read the immutable published result and its stored comparison decision.</summary>
    [HttpGet("health-results/publication")]
    [ProducesResponseType<PlantHealthPublishedResultResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> GetPublished(Guid orderId, CancellationToken token) =>
        (await published.GetPublishedAsync(orderId, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Atomically publish the approved preliminary result and complete its Order.</summary>
    [HttpPost("health-results/publish")]
    [ProducesResponseType<PublishPlantHealthSurveyResultResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> PublishResult(Guid orderId, PublishPlantHealthSurveyResultRequest request, CancellationToken token) =>
        (await publisher.PublishResultAsync(orderId, request, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Approve the exact reviewed preliminary candidate without publishing it.</summary>
    [HttpPost("health-results/approve")]
    [ProducesResponseType<ApprovePlantHealthSurveyResultResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> ApproveResult(Guid orderId, ApprovePlantHealthSurveyResultRequest request, CancellationToken token) =>
        (await reviews.ApproveResultAsync(orderId, request, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read approval metadata for the currently assigned Manager.</summary>
    [HttpGet("health-results/approval")]
    [ProducesResponseType<ApprovePlantHealthSurveyResultResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> GetApproval(Guid orderId, CancellationToken token) =>
        (await reviews.GetApprovalAsync(orderId, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read the assigned Farm's pending findings and review versions.</summary>
    [HttpGet("health-findings")]
    [ProducesResponseType<HealthReviewPage>(StatusCodes.Status200OK)]
    public async Task<IResult> GetFindings(Guid orderId, [FromQuery] HealthReviewQuery query, CancellationToken token) =>
        (await reviews.GetAsync(orderId, "findings", query, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read immutable review history for one source finding in a selected batch.</summary>
    [HttpGet("health-findings/{findingId:guid}/reviews")]
    [ProducesResponseType<HealthReviewPage>(StatusCodes.Status200OK)]
    public async Task<IResult> GetHistory(Guid orderId, Guid findingId, [FromQuery] HealthReviewQuery query, CancellationToken token) =>
        (await reviews.GetHistoryAsync(orderId, findingId, query, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Review one finding while preserving its original AI output.</summary>
    [HttpPost("health-findings/{findingId:guid}/review")]
    [ProducesResponseType<HealthReviewOutcome>(StatusCodes.Status200OK)]
    public async Task<IResult> ReviewFinding(Guid orderId, Guid findingId, HealthFindingReviewRequest request, CancellationToken token)
    {
        var batch = new HealthFindingBatchRequest(request.OperationId, request.BatchId, request.ExpectedResultVersion,
            [new(findingId, request.ExpectedSourceVersion, request.ExpectedReviewVersion, request.Decision, request.Reason,
                request.EvidenceMediaIds, request.CorrectedClassCode, request.CorrectedPlantId, request.CorrectedPosition)]);
        return (await reviews.ReviewFindingsAsync(orderId, batch, token)).ToHttpResult(HttpContext, Results.Ok);
    }

    /// <summary>Review up to 100 findings atomically within one active batch.</summary>
    [HttpPost("health-findings/review-batch")]
    [ProducesResponseType<HealthReviewOutcome>(StatusCodes.Status200OK)]
    public async Task<IResult> ReviewBatch(Guid orderId, HealthFindingBatchRequest request, CancellationToken token) =>
        (await reviews.ReviewFindingsAsync(orderId, request, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read unresolved Plant Health boundary evidence.</summary>
    [HttpGet("health-boundary-exceptions")]
    [ProducesResponseType<HealthReviewPage>(StatusCodes.Status200OK)]
    public async Task<IResult> GetExceptions(Guid orderId, [FromQuery] HealthReviewQuery query, CancellationToken token) =>
        (await reviews.GetAsync(orderId, "exceptions", query, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Resolve or retain a boundary exception against a current finding review.</summary>
    [HttpPost("health-boundary-exceptions/{exceptionId:guid}/review")]
    [ProducesResponseType<HealthReviewOutcome>(StatusCodes.Status200OK)]
    public async Task<IResult> ReviewException(Guid orderId, Guid exceptionId, HealthBoundaryReviewRequest request, CancellationToken token) =>
        (await reviews.ReviewBoundaryAsync(orderId, exceptionId, request, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read proposed Disease Zones with current review and guidance decisions.</summary>
    [HttpGet("disease-zones")]
    [ProducesResponseType<HealthReviewPage>(StatusCodes.Status200OK)]
    public async Task<IResult> GetZones(Guid orderId, [FromQuery] HealthReviewQuery query, CancellationToken token) =>
        (await reviews.GetAsync(orderId, "zones", query, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Review zone geometry, class and accepted finding memberships.</summary>
    [HttpPost("disease-zones/{zoneId:guid}/review")]
    [ProducesResponseType<HealthReviewOutcome>(StatusCodes.Status200OK)]
    public async Task<IResult> ReviewZone(Guid orderId, Guid zoneId, HealthZoneReviewRequest request, CancellationToken token) =>
        (await reviews.ReviewZoneAsync(orderId, zoneId, request, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read active cited guidance applicable to the reviewed zone class.</summary>
    [HttpGet("disease-zones/{zoneId:guid}/guidance-options")]
    [ProducesResponseType<IReadOnlyList<HealthGuidanceOption>>(StatusCodes.Status200OK)]
    public async Task<IResult> GuidanceOptions(Guid orderId, Guid zoneId, CancellationToken token) =>
        (await reviews.GetGuidanceOptionsAsync(orderId, zoneId, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Select cited guidance, reject all choices, or request more evidence.</summary>
    [HttpPost("disease-zones/{zoneId:guid}/guidance-review")]
    [ProducesResponseType<HealthReviewOutcome>(StatusCodes.Status200OK)]
    public async Task<IResult> ReviewGuidance(Guid orderId, Guid zoneId, HealthGuidanceReviewRequest request, CancellationToken token) =>
        (await reviews.ReviewGuidanceAsync(orderId, zoneId, request, token)).ToHttpResult(HttpContext, Results.Ok);

    /// <summary>Read current blockers for result approval. This action does not approve or publish.</summary>
    [HttpGet("health-results/review-summary")]
    [ProducesResponseType<HealthReviewSummary>(StatusCodes.Status200OK)]
    public async Task<IResult> ReviewSummary(Guid orderId, CancellationToken token) =>
        (await reviews.GetSummaryAsync(orderId, token)).ToHttpResult(HttpContext, Results.Ok);
}
