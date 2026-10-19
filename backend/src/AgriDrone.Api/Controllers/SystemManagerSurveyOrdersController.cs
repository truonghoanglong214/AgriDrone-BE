using System.ComponentModel.DataAnnotations;
using AgriDrone.Api.Contracts.Surveys.Orders;
using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderReadiness;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetAssignedSurveyOrders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.ConfirmSurveyOrderPoleCount;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetSurveyOrderBoundaryScope;
using AgriDrone.Modules.Surveys.Application.Features.Orders.VerifySurveyOrderBoundaryAndScope;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.BoundaryCapture;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ScopeAdjustments.RequestScopeAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.RequestExtraFlight;
using AgriDrone.Modules.Surveys.Application.Features.Adjustments.ExtraFlights.ConsumeExtraFlight;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Pagination;
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
    /// <summary>Lấy danh sách SurveyOrder thuộc các farm đang được phân công.</summary>
    /// <remarks>
    /// Backend tự xác định SystemManager từ access token. Có thể lọc theo status;
    /// kết quả chứa SurveyOrderId và Version để tiếp tục các bước boundary-scope.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<AssignedSurveyOrderApiResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IResult> GetAssignedOrders(
        [FromQuery] GetAssignedSurveyOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetAssignedSurveyOrdersQuery(
                MapStatus(request.Status),
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            page => Results.Ok(SurveyOrderResponseMapper.ToResponse(page)));
    }

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

    /// <summary>Bắt đầu phiên chấm boundary trong lịch Baseline đã được Owner xác nhận.</summary>
    [HttpPost("{orderId:guid}/boundary-capture-sessions")]
    [ProducesResponseType<BoundaryCaptureApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> StartBoundaryCapture(
        [FromRoute] Guid orderId,
        [FromBody] StartBoundaryCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new StartBoundaryCaptureCommand(
                orderId,
                request.DroneId!.Value,
                request.OperationId!.Value,
                request.ExpectedOrderVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                MapBoundaryCapture(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Đồng bộ idempotent các mẫu drone-GNSS theo thứ tự.</summary>
    [HttpPost("{orderId:guid}/boundary-capture-sessions/{sessionId:guid}/samples:sync")]
    [ProducesResponseType<BoundaryCaptureApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> SyncBoundaryCaptureSamples(
        [FromRoute] Guid orderId,
        [FromRoute] Guid sessionId,
        [FromBody] SyncBoundaryCaptureSamplesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new SyncBoundaryCaptureSamplesCommand(
                orderId,
                sessionId,
                request.ExpectedSessionVersion!.Value,
                request.Samples.Select(item => new BoundaryCaptureSampleInput(
                    item.ClientOperationId!.Value,
                    item.SequenceNumber,
                    item.Latitude,
                    item.Longitude,
                    item.HorizontalAccuracyMeters,
                    item.CapturedAt!.Value)).ToArray()),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapBoundaryCapture(response)));
    }

    /// <summary>Đóng vòng GNSS, approve immutable FarmBoundary và giữ Order chờ route.</summary>
    [HttpPost("{orderId:guid}/boundary-capture-sessions/{sessionId:guid}/finalize")]
    [ProducesResponseType<BoundaryCaptureApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> FinalizeBoundaryCapture(
        [FromRoute] Guid orderId,
        [FromRoute] Guid sessionId,
        [FromBody] FinalizeBoundaryCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new FinalizeBoundaryCaptureCommand(
                orderId,
                sessionId,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedSessionVersion!.Value,
                request.ExpectedCurrentBoundaryVersionNumber),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapBoundaryCapture(response)));
    }

    /// <summary>Hủy phiên capture nhưng giữ toàn bộ evidence đã đồng bộ.</summary>
    [HttpPost("{orderId:guid}/boundary-capture-sessions/{sessionId:guid}/abort")]
    [ProducesResponseType<BoundaryCaptureApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> AbortBoundaryCapture(
        [FromRoute] Guid orderId,
        [FromRoute] Guid sessionId,
        [FromBody] AbortBoundaryCaptureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AbortBoundaryCaptureCommand(
                orderId,
                sessionId,
                request.Reason,
                request.ExpectedSessionVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapBoundaryCapture(response)));
    }

    /// <summary>Lấy thông tin chi tiết và các samples đã đồng bộ của phiên capture.</summary>
    [HttpGet("{orderId:guid}/boundary-capture-sessions/{sessionId:guid}")]
    [ProducesResponseType<BoundaryCaptureApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBoundaryCaptureSession(
        [FromRoute] Guid orderId,
        [FromRoute] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AgriDrone.Modules.Surveys.Application.Features.BoundaryCapture.GetBoundaryCaptureSession.GetBoundaryCaptureSessionQuery(
                orderId,
                sessionId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapBoundaryCapture(response)));
    }

    /// <summary>SystemManager duyệt route plan tự động từ BE2 và chuyển Order sang BaselineReady.</summary>
    [HttpPost("{orderId:guid}/baseline-route-plans/{routePlanId:guid}/approve")]
    [ProducesResponseType<BoundaryCaptureApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> ApproveBaselineRoutePlan(
        [FromRoute] Guid orderId,
        [FromRoute] Guid routePlanId,
        [FromBody] ApproveBaselineRoutePlanRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApproveBaselineRoutePlanCommand(
                orderId,
                request.SessionId!.Value,
                request.FarmBoundaryVersionId!.Value,
                routePlanId,
                request.RoutePlanVersion!.Value,
                request.SafetyInsetMeters,
                request.IsWithinSafetyInset,
                request.ExpectedOrderVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapBoundaryCapture(response)));
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

    /// <summary>Yêu cầu điều chỉnh scope (boundary/pole count) có lưu audit evidence.</summary>
    [HttpPost("{orderId:guid}/scope-adjustments")]
    [ProducesResponseType<ScopeAdjustmentApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RequestScopeAdjustment(
        [FromRoute] Guid orderId,
        [FromBody] RequestScopeAdjustmentApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestScopeAdjustmentCommand(
                orderId,
                request.Reason,
                request.BeforeBoundaryVersionId,
                request.AfterBoundaryVersionId,
                request.BeforePoleCount,
                request.AfterPoleCount,
                request.FinancialImpact,
                request.Currency,
                request.EvidenceReference,
                request.ExpectedOrderVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                MapScopeAdjustment(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Yêu cầu chuyến bay bổ sung (extra flight) trong cùng visit hoặc rescan.</summary>
    [HttpPost("{orderId:guid}/extra-flights")]
    [ProducesResponseType<ExtraFlightApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RequestExtraFlight(
        [FromRoute] Guid orderId,
        [FromBody] RequestExtraFlightApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestExtraFlightCommand(
                orderId,
                request.FarmId,
                request.SourceMissionId,
                request.Reason,
                request.EvidenceReference,
                request.CostImpact,
                request.Currency),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                MapExtraFlight(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Ghi nhận chuyến bay bổ sung đã được thực thi bởi mission.</summary>
    [HttpPost("{orderId:guid}/extra-flights/{requestId:guid}/consume")]
    [ProducesResponseType<ExtraFlightApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ConsumeExtraFlight(
        [FromRoute] Guid orderId,
        [FromRoute] Guid requestId,
        [FromBody] ConsumeExtraFlightApiRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConsumeExtraFlightCommand(
                orderId,
                requestId,
                request.ExtraMissionId,
                request.ExpectedVersion!.Value),
            cancellationToken);

        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(MapExtraFlight(response)));
    }

    private static SurveyOrderStatus? MapStatus(
        SurveyOrderStatusFilterValue? status) =>
        status switch
        {
            null => null,
            SurveyOrderStatusFilterValue.PENDING_BOUNDARY_VERIFICATION =>
                SurveyOrderStatus.PendingBoundaryVerification,
            SurveyOrderStatusFilterValue.AWAITING_BASELINE_APPOINTMENT =>
                SurveyOrderStatus.AwaitingBaselineAppointment,
            SurveyOrderStatusFilterValue.AWAITING_BOUNDARY_CAPTURE =>
                SurveyOrderStatus.AwaitingBoundaryCapture,
            SurveyOrderStatusFilterValue.BASELINE_READY =>
                SurveyOrderStatus.BaselineReady,
            SurveyOrderStatusFilterValue.BASELINE_IN_PROGRESS =>
                SurveyOrderStatus.BaselineInProgress,
            SurveyOrderStatusFilterValue.AWAITING_BASELINE_REVIEW =>
                SurveyOrderStatus.AwaitingBaselineReview,
            SurveyOrderStatusFilterValue.AWAITING_PRICING =>
                SurveyOrderStatus.AwaitingPricing,
            SurveyOrderStatusFilterValue.AWAITING_PAID_APPOINTMENT =>
                SurveyOrderStatus.AwaitingPaidAppointment,
            SurveyOrderStatusFilterValue.AWAITING_PAYMENT =>
                SurveyOrderStatus.AwaitingPayment,
            SurveyOrderStatusFilterValue.READY_FOR_PAID_SERVICE =>
                SurveyOrderStatus.ReadyForPaidService,
            SurveyOrderStatusFilterValue.IN_PROGRESS =>
                SurveyOrderStatus.InProgress,
            SurveyOrderStatusFilterValue.PENDING_REVIEW =>
                SurveyOrderStatus.PendingReview,
            SurveyOrderStatusFilterValue.COMPLETED =>
                SurveyOrderStatus.Completed,
            SurveyOrderStatusFilterValue.CANCELLED =>
                SurveyOrderStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unsupported survey-order status.")
        };

    private static BoundaryCaptureApiResponse MapBoundaryCapture(
        BoundaryCaptureResponse response) => new(
            response.SurveyOrderId,
            response.SessionId,
            response.StartOperationId,
            response.FarmId,
            response.AppointmentId,
            response.AppointmentVersion,
            response.SystemManagerProfileId,
            response.DroneId,
            response.Status.ToString(),
            response.FarmBoundaryVersionId,
            response.BaselineRoutePlanId,
            response.OrderStatus.ToString(),
            response.OrderVersion,
            response.SessionVersion,
            response.Samples.Select(item => new BoundaryCaptureSampleApiResponse(
                item.Id,
                item.ClientOperationId,
                item.SequenceNumber,
                item.Latitude,
                item.Longitude,
                item.HorizontalAccuracyMeters,
                item.CapturedAt,
                item.ReceivedAt)).ToArray());

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

public enum PaidSurveyMissionPurpose
{
    PlantHealth
}
