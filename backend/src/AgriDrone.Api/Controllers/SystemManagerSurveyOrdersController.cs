using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;
using AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;
using AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/system-manager/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.SystemManager)]
public sealed class SystemManagerSurveyOrdersController(ISender sender)
    : ControllerBase
{
    /// <summary>
    /// Lấy Order, approved FarmBoundary và Zone scope để SystemManager review.
    /// </summary>
    [HttpGet("{orderId:guid}/boundary-scope")]
    [ProducesResponseType<GetSurveyOrderBoundaryScopeApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBoundaryScope(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyOrderBoundaryScopeQuery(orderId),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyOrderResponseMapper.ToResponse(response)));
    }

    /// <summary>
    /// Xác minh FarmBoundary và scope của SurveyOrder được phân công.
    /// </summary>
    /// <remarks>
    /// Geometry là GeoJSON Polygon WGS84 (SRID 4326). Backend re-check primary
    /// manager assignment, Zone containment, current base map và confirmed pole
    /// inventory trong cùng transaction; client không quyết định baseline state.
    /// </remarks>
    [HttpPost("{orderId:guid}/boundary-scope/verify")]
    [ProducesResponseType<VerifySurveyOrderBoundaryAndScopeApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> VerifyBoundaryAndScope(
        [FromRoute] Guid orderId,
        [FromBody] VerifySurveyOrderBoundaryAndScopeRequest request,
        CancellationToken cancellationToken)
    {
        var boundary = GeoJsonGeometryMapper.ToPolygon(request.Boundary)!;
        var result = await sender.Send(
            new VerifySurveyOrderBoundaryAndScopeCommand(
                orderId,
                boundary,
                request.Reason,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedCurrentBoundaryVersionNumber),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyOrderResponseMapper.ToResponse(response)));
    }

    /// <summary>Xác nhận active pole count và khóa snapshot giá theo trụ.</summary>
    /// <remarks>
    /// Backend tự đọc current published base map, active Plant inventory trong
    /// approved boundary/Zone scope và bảng giá đang hiệu lực. Client chỉ xác nhận
    /// expected map/Order version cùng evidence; không gửi count hoặc đơn giá.
    /// </remarks>
    [HttpPost("{orderId:guid}/pole-count/confirm")]
    [ProducesResponseType<ConfirmSurveyOrderPoleCountApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ConfirmPoleCount(
        [FromRoute] Guid orderId,
        [FromBody] ConfirmSurveyOrderPoleCountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfirmSurveyOrderPoleCountCommand(
                orderId,
                request.ExpectedFarmBaseMapVersionId!.Value,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyOrderResponseMapper.ToResponse(response)));
    }

    /// <summary>Đề xuất hoặc đề xuất lại lịch theo purpose của SurveyOrder.</summary>
    /// <remarks>
    /// Đề xuất lại yêu cầu ID/version của appointment đang RescheduleRequested.
    /// Version cũ được giữ lịch sử và chuyển Superseded; khung giờ không bị ghi đè.
    /// </remarks>
    [HttpPost("{orderId:guid}/appointments")]
    [ProducesResponseType<SurveyAppointmentApiResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ProposeAppointment(
        [FromRoute] Guid orderId,
        [FromBody] ProposeSurveyAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ProposeSurveyAppointmentCommand(
                orderId,
                SurveyAppointmentResponseMapper.ToDomain(
                    request.Purpose!.Value),
                request.ProposedStartAt!.Value,
                request.ProposedEndAt!.Value,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedCurrentAppointmentId,
                request.ExpectedCurrentAppointmentVersion),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                SurveyAppointmentResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Đánh giá readiness Baseline Mapping từ trạng thái server.</summary>
    /// <remarks>
    /// Mission phải liên kết Order, đúng purpose, nằm trong appointment window;
    /// drone, assigned manager và completed pre-flight được đọc trực tiếp từ DB.
    /// Payment không tham gia Baseline readiness.
    /// </remarks>
    [HttpGet("{orderId:guid}/readiness/baseline-mapping")]
    [ProducesResponseType<SurveyOrderReadinessApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBaselineReadiness(
        [FromRoute] Guid orderId,
        [FromQuery, Required] Guid? missionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyOrderReadinessQuery(
                orderId,
                missionId!.Value,
                SurveyOperationPurpose.BaselineMapping),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyAppointmentResponseMapper.ToResponse(response)));
    }

    /// <summary>Đánh giá authoritative readiness cho paid-service mission.</summary>
    [HttpGet("{orderId:guid}/readiness/paid-service")]
    [ProducesResponseType<SurveyOrderReadinessApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetPaidServiceReadiness(
        [FromRoute] Guid orderId,
        [FromQuery, Required] Guid? missionId,
        [FromQuery, Required] PaidSurveyMissionPurpose? purpose,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyOrderReadinessQuery(
                orderId,
                missionId!.Value,
                SurveyOperationPurpose.PlantHealth),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyAppointmentResponseMapper.ToResponse(response)));
    }

    /// <summary>Yêu cầu điều chỉnh pole count/price snapshot có lưu lịch sử.</summary>
    [HttpPost("{orderId:guid}/price-adjustments")]
    [ProducesResponseType<RequestedPriceAdjustmentApiResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RequestPriceAdjustment(
        [FromRoute] Guid orderId,
        [FromBody] RequestPriceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestPriceAdjustmentCommand(
                orderId,
                request.NewConfirmedPoleCount!.Value,
                request.Reason,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                SurveyPaymentResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }
}

public enum PaidSurveyMissionPurpose
{
    PlantHealth
}
