using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApprovePriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-admin/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemAdmin)]
public sealed class SystemSurveyOrdersController(ISender sender)
    : ControllerBase
{
    [HttpPost("{orderId:guid}/price-adjustments/{adjustmentId:guid}/approve")]
    [ProducesResponseType<ReviewedPriceAdjustmentApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ApprovePriceAdjustment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid adjustmentId,
        [FromBody] ReviewPriceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApprovePriceAdjustmentCommand(
                orderId,
                adjustmentId,
                request.Reason,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAdjustmentVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyPaymentResponseMapper.ToResponse(response)));
    }

    [HttpPost("{orderId:guid}/price-adjustments/{adjustmentId:guid}/reject")]
    [ProducesResponseType<ReviewedPriceAdjustmentApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RejectPriceAdjustment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid adjustmentId,
        [FromBody] ReviewPriceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RejectPriceAdjustmentCommand(
                orderId,
                adjustmentId,
                request.Reason,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAdjustmentVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyPaymentResponseMapper.ToResponse(response)));
    }

    [HttpPost("{orderId:guid}/price-adjustments/{adjustmentId:guid}/apply")]
    [ProducesResponseType<AppliedPriceAdjustmentApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ApplyPriceAdjustment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid adjustmentId,
        [FromBody] ApplyPriceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApplyPriceAdjustmentCommand(
                orderId,
                adjustmentId,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAdjustmentVersion!.Value,
                request.ExpectedPaymentVersion),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyPaymentResponseMapper.ToResponse(response)));
    }
}
