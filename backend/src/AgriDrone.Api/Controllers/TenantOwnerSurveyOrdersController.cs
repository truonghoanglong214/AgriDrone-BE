using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrders;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetTenantOwnerSurveyOrder;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/tenant-owner/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class TenantOwnerSurveyOrdersController(
    ISender sender,
    IQualityRefundWorkflow qualityRefunds)
    : ControllerBase
{
    /// <summary>Lấy danh sách SurveyOrder thuộc tenant hiện tại của Owner.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AgriDrone.Api.Contracts.Surveys.Orders.TenantOwnerSurveyOrderApiResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IResult> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] SurveyOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetTenantOwnerSurveyOrdersQuery(pageNumber, pageSize, status),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            page => Results.Ok(
                TenantOwnerSurveyOrderResponseMapper.ToResponse(page)));
    }

    /// <summary>Lấy chi tiết SurveyOrder cùng số tiền và lịch hẹn hiện tại.</summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType<AgriDrone.Api.Contracts.Surveys.Orders.TenantOwnerSurveyOrderApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetOrder(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetTenantOwnerSurveyOrderQuery(orderId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            item => Results.Ok(
                TenantOwnerSurveyOrderResponseMapper.ToResponse(item)));
    }

    /// <summary>Xác nhận appointment hiện hành của SurveyOrder thuộc Tenant.</summary>
    [HttpPost("{orderId:guid}/appointments/{appointmentId:guid}/confirm")]
    [ProducesResponseType<SurveyAppointmentApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> ConfirmAppointment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid appointmentId,
        [FromBody] ConfirmSurveyAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfirmSurveyAppointmentCommand(
                orderId,
                appointmentId,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAppointmentVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyAppointmentResponseMapper.ToResponse(response)));
    }

    /// <summary>Yêu cầu đổi lịch và đóng readiness của appointment đã confirm.</summary>
    [HttpPost("{orderId:guid}/appointments/{appointmentId:guid}/request-reschedule")]
    [ProducesResponseType<SurveyAppointmentApiResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> RequestReschedule(
        [FromRoute] Guid orderId,
        [FromRoute] Guid appointmentId,
        [FromBody] RequestSurveyAppointmentRescheduleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestSurveyAppointmentRescheduleCommand(
                orderId,
                appointmentId,
                request.Reason,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAppointmentVersion!.Value),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyAppointmentResponseMapper.ToResponse(response)));
    }

    /// <summary>Khởi tạo thanh toán theo snapshot giá hiện hành của SurveyOrder.</summary>
    /// <remarks>
    /// Amount và currency luôn được lấy từ snapshot phía server. TenantOwner chỉ
    /// gửi expected Order version và không thể tự xác nhận thanh toán.
    /// </remarks>
    [HttpPost("{orderId:guid}/payments")]
    [ProducesResponseType<InitiateSurveyPaymentApiResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> InitiatePayment(
        [FromRoute] Guid orderId,
        [FromBody] InitiateSurveyPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParsePaymentPurpose(request.Purpose, out var purpose))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid payment purpose",
                detail: $"The payment purpose '{request.Purpose}' is invalid. Allowed values: BASELINE_DEPOSIT, FINAL_BALANCE.");
        }

        var result = await sender.Send(
            new InitiateSurveyPaymentCommand(
                orderId,
                request.ExpectedOrderVersion!.Value,
                purpose),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                SurveyPaymentResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Gets the payment history, including baseline deposits and final balances.</summary>
    [HttpGet("{orderId:guid}/payments")]
    [ProducesResponseType<SurveyPaymentHistoryApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetPaymentHistory(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetSurveyPaymentHistoryQuery(orderId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyPaymentResponseMapper.ToResponse(response)));
    }

    /// <summary>Owner báo kết quả Plant Health đã công bố sai theo từng trụ cây.</summary>
    [HttpPost("{orderId:guid}/quality-refunds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IResult> ReportQualityRefund(
        [FromRoute] Guid orderId,
        [FromBody] ReportQualityRefundRequest request,
        CancellationToken cancellationToken)
    {
        var issues = request.Issues?.Select(item => item is null
            ? null!
            : new ReportedPoleIssue(item.PoleCode, item.IssueType, item.Details)).ToArray() ?? [];
        var result = await qualityRefunds.ReportAsync(orderId, issues,
            request.Description, request.EvidenceReference,
            request.RecipientBankName, request.RecipientAccountNumber,
            request.RecipientAccountName, cancellationToken);
        return result.ToHttpResult(HttpContext,
            response => Results.Json(response, statusCode: StatusCodes.Status201Created));
    }

    /// <summary>Owner xem các báo cáo sai kết quả và trạng thái hoàn tiền của đơn.</summary>
    [HttpGet("{orderId:guid}/quality-refunds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IResult> GetQualityRefunds(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await qualityRefunds.GetOwnerCasesAsync(orderId, cancellationToken);
        return result.ToHttpResult(HttpContext, Results.Ok);
    }

    private static bool TryParsePaymentPurpose(string? purpose, out PaymentPurpose parsedPurpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            parsedPurpose = PaymentPurpose.FinalBalance;
            return true;
        }

        switch (purpose.Trim().ToUpperInvariant())
        {
            case "BASELINE_DEPOSIT":
            case "BASELINEDEPOSIT":
                parsedPurpose = PaymentPurpose.BaselineDeposit;
                return true;
            case "FINAL_BALANCE":
            case "FINALBALANCE":
                parsedPurpose = PaymentPurpose.FinalBalance;
                return true;
            default:
                parsedPurpose = default;
                return false;
        }
    }
}
