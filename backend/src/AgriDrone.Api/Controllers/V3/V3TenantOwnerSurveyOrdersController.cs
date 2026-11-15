using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Mapping;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriDrone.Api.Controllers.V3;

[ApiController]
[Route("api/v3/tenant-owner/survey-orders")]
[Authorize(Policy = AccessAuthorizationPolicies.TenantOwner)]
public sealed class V3TenantOwnerSurveyOrdersController(
    ISender sender,
    ISurveyCaptureExecutionCoordinator captureCoordinator,
    IExecutionContext executionContext)
    : ControllerBase
{
    [HttpPost("{orderId:guid}/capture-appointments/{appointmentId:guid}/confirm")]
    [ProducesResponseType<SurveyAppointmentApiResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> ConfirmCaptureAppointment(
        [FromRoute] Guid orderId,
        [FromRoute] Guid appointmentId,
        [FromBody] ConfirmV3CaptureAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ConfirmSurveyAppointmentCommand(
                orderId,
                appointmentId,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAppointmentVersion!.Value,
                ContractScope.V3),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyAppointmentResponseMapper.ToResponse(response)));
    }

    [HttpPost("{orderId:guid}/capture-appointments/{appointmentId:guid}/request-reschedule")]
    [ProducesResponseType<SurveyAppointmentApiResponse>(StatusCodes.Status200OK)]
    public async Task<IResult> RequestCaptureAppointmentReschedule(
        [FromRoute] Guid orderId,
        [FromRoute] Guid appointmentId,
        [FromBody] RequestV3CaptureAppointmentRescheduleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestSurveyAppointmentRescheduleCommand(
                orderId,
                appointmentId,
                request.Reason,
                request.ExpectedOrderVersion!.Value,
                request.ExpectedAppointmentVersion!.Value,
                ContractScope.V3),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyAppointmentResponseMapper.ToResponse(response)));
    }

    [HttpPost("{orderId:guid}/deposit-checkouts")]
    [ProducesResponseType<InitiateSurveyPaymentApiResponse>(StatusCodes.Status201Created)]
    public async Task<IResult> InitiateOrderDeposit(
        [FromRoute] Guid orderId,
        [FromBody] InitiateV3OrderDepositRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new InitiateSurveyPaymentCommand(
                orderId,
                request.ExpectedOrderVersion!.Value,
                PaymentPurpose.OrderDeposit),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Json(
                SurveyPaymentResponseMapper.ToResponse(response),
                statusCode: StatusCodes.Status201Created));
    }

    [HttpPost("{orderId:guid}/cancellation")]
    public async Task<IResult> CancelBeforeCapture(
        [FromRoute] Guid orderId,
        [FromBody] CancelV3OwnerOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!executionContext.IsInitialized ||
            executionContext.ActorId is not Guid actorId ||
            actorId == Guid.Empty ||
            executionContext.CorrelationId == Guid.Empty)
        {
            return Results.Forbid();
        }

        var result = await captureCoordinator.CancelOrderAsync(
            new CancelCaptureOrderRequest(
                orderId,
                request.ExpectedOrderVersion!.Value,
                actorId,
                actorId,
                CaptureCancellationDecisionKind.CustomerBeforeCapture,
                0m,
                null,
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
}
