using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Mapping;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/system-admin/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class V3SystemAdminSurveyOrdersController(
    ISender sender,
    ISurveyCaptureExecutionCoordinator captureCoordinator,
    IExecutionContext executionContext) : ControllerBase
{
    [HttpPost("{orderId:guid}/cancellation-decisions")]
    public async Task<IResult> DecideCancellation(
        [FromRoute] Guid orderId,
        [FromBody] DecideV3OrderCancellationRequest request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            actorId == Guid.Empty ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Results.Forbid();
        }

        if (request.DecisionKind == CaptureCancellationDecisionKind.CustomerBeforeCapture)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DecisionKind)] =
                    ["CustomerBeforeCapture must use the Tenant Owner cancellation route."]
            });
        }

        var result = await captureCoordinator.CancelOrderAsync(
            new CancelCaptureOrderRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                actorId,
                request.RequesterId!.Value,
                request.DecisionKind!.Value,
                request.RequestedRetentionAmount,
                request.InputApprovalEvidenceId,
                request.ReasonCode,
                request.ReasonDescription,
                executionContext.CorrelationId,
                request.OperationId!.Value,
                request.PayloadFingerprint),
            cancellationToken);
        return result.Success
            ? Results.Ok(result)
            : Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: result.ErrorCode ?? "V3 cancellation conflict",
                detail: result.ErrorMessage);
    }

    /// <summary>Đối soát và xử lý hold tài chính (KeepExcluded / AllocateToCurrentInvoice / RouteToRefund) cho đơn V3.</summary>
    [HttpPost("{orderId:guid}/funding-reconciliations")]
    [ProducesResponseType<V3FundingReconciliationApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> ReconcileFunding(
        [FromRoute] Guid orderId,
        [FromBody] ReconcileV3SurveyOrderFundingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            V3SurveyResultAndInvoiceMapper.ToCommand(orderId, request),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(V3SurveyResultAndInvoiceMapper.ToResponse(response)));
    }
}
