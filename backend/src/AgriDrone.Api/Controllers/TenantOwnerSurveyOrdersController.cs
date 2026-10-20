using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/tenant-owner/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class TenantOwnerSurveyOrdersController(ISender sender)
    : ControllerBase
{
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
        var purpose = ParsePaymentPurpose(request.Purpose);
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

    private static PaymentPurpose ParsePaymentPurpose(string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return PaymentPurpose.FinalBalance;
        }

        return purpose.Trim().ToUpperInvariant() switch
        {
            "BASELINE_DEPOSIT" or "BASELINEDEPOSIT" => PaymentPurpose.BaselineDeposit,
            "FINAL_BALANCE" or "FINALBALANCE" => PaymentPurpose.FinalBalance,
            _ => Enum.TryParse<PaymentPurpose>(purpose, true, out var parsed) ? parsed : PaymentPurpose.FinalBalance
        };
    }
}
