using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApprovePriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ReviewScopeAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.ApplyScopeAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.CreateAdjustmentSettlement;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.ConfirmAdjustmentSettlement;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.AdjustmentSettlements.FailAdjustmentSettlement;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ReviewExtraFlight;
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

    /// <summary>System Admin review và approve/reject scope adjustment.</summary>
    [HttpPost("{orderId:guid}/scope-adjustments/{adjustmentId:guid}/review")]
    [ProducesResponseType<ScopeAdjustmentApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ReviewScopeAdjustment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid adjustmentId,
        [FromBody] ReviewScopeAdjustmentApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReviewScopeAdjustmentCommand(
                orderId,
                adjustmentId,
                request.Approved,
                request.ReviewReason,
                request.ExpectedAdjustmentVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapScopeAdjustment(response)));
    }

    /// <summary>System Admin áp dụng scope adjustment đã được duyệt.</summary>
    [HttpPost("{orderId:guid}/scope-adjustments/{adjustmentId:guid}/apply")]
    [ProducesResponseType<ScopeAdjustmentApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ApplyScopeAdjustment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid adjustmentId,
        [FromBody] ApplyScopeAdjustmentApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApplyScopeAdjustmentCommand(
                orderId,
                adjustmentId,
                request.ExpectedAdjustmentVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapScopeAdjustment(response)));
    }

    /// <summary>Tạo financial settlement (additional payment / refund) cho adjustment.</summary>
    [HttpPost("adjustment-settlements")]
    [ProducesResponseType<AdjustmentSettlementApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> CreateAdjustmentSettlement(
        [FromBody] CreateAdjustmentSettlementApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateAdjustmentSettlementCommand(
                request.PriceAdjustmentId,
                request.Type,
                request.Amount,
                request.Currency,
                request.IdempotencyKey),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                MapSettlement(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Xác nhận settlement đã hoàn tất thành công.</summary>
    [HttpPost("adjustment-settlements/{settlementId:guid}/confirm")]
    [ProducesResponseType<AdjustmentSettlementApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ConfirmAdjustmentSettlement(
        [FromRoute] Guid settlementId,
        [FromBody] ConfirmAdjustmentSettlementApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfirmAdjustmentSettlementCommand(
                settlementId,
                request.ProviderReference,
                request.ExpectedVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapSettlement(response)));
    }

    /// <summary>Ghi nhận settlement thất bại.</summary>
    [HttpPost("adjustment-settlements/{settlementId:guid}/fail")]
    [ProducesResponseType<AdjustmentSettlementApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> FailAdjustmentSettlement(
        [FromRoute] Guid settlementId,
        [FromBody] FailAdjustmentSettlementApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new FailAdjustmentSettlementCommand(
                settlementId,
                request.FailureReason,
                request.ExpectedVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapSettlement(response)));
    }

    /// <summary>System Admin review chuyến bay bổ sung (extra flight).</summary>
    [HttpPost("{orderId:guid}/extra-flights/{requestId:guid}/review")]
    [ProducesResponseType<ExtraFlightApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ReviewExtraFlight(
        [FromRoute] Guid orderId,
        [FromRoute] Guid requestId,
        [FromBody] ReviewExtraFlightApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ReviewExtraFlightCommand(
                orderId,
                requestId,
                request.Approved,
                request.ExpectedVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapExtraFlight(response)));
    }

    private static ScopeAdjustmentApiResponse MapScopeAdjustment(
        ScopeAdjustmentResponse response) =>
        new(response.Id,
            response.SurveyOrderId,
            response.Reason.ToString(),
            response.BeforeBoundaryVersionId,
            response.AfterBoundaryVersionId,
            response.BeforePoleCount,
            response.AfterPoleCount,
            response.FinancialImpact,
            response.Currency,
            response.EvidenceReference,
            response.Status.ToString(),
            response.RequestedBy,
            response.ReviewedBy,
            response.ReviewedAt,
            response.ReviewReason,
            response.Version,
            response.CreatedAt);

    private static AdjustmentSettlementApiResponse MapSettlement(
        AdjustmentSettlementResponse response) =>
        new(response.Id,
            response.PriceAdjustmentId,
            response.Type.ToString(),
            response.Status.ToString(),
            response.Amount,
            response.Currency,
            response.IdempotencyKey,
            response.ProviderReference,
            response.FailureReason,
            response.Version,
            response.CreatedAt,
            response.UpdatedAt);

    private static ExtraFlightApiResponse MapExtraFlight(
        ExtraFlightRequestResponse response) =>
        new(response.Id,
            response.SurveyOrderId,
            response.FarmId,
            response.SourceMissionId,
            response.ExtraMissionId,
            response.Reason.ToString(),
            response.EvidenceReference,
            response.CostImpact,
            response.Currency,
            response.Status.ToString(),
            response.RequestedBy,
            response.ReviewedBy,
            response.ReviewedAt,
            response.Version,
            response.CreatedAt);
}
