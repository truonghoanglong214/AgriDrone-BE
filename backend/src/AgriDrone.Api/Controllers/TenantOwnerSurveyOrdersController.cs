using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
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
}
