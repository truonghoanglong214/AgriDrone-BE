using AgriDrone.Api.Contracts.Surveys.Appointments;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Contracts.Surveys.V3;
using AgriDrone.Api.Mapping;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ConfirmSurveyAppointment;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.RequestSurveyAppointmentReschedule;
using AgriDrone.Modules.Surveys.Application.Features.Invoices.GetTenantOwnerSurveyOrderInvoice;
using AgriDrone.Modules.Surveys.Application.Features.Orders.GetV3TenantOwnerOrders;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateBalanceCheckout;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Authorization;
using AgriDrone.SharedInfrastructure.Http;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
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
    /// <summary>Lấy danh sách đơn khảo sát V3 và lịch bay hiện hành của tenant.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<V3TenantOwnerSurveyOrderItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IResult> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] SurveyOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetV3TenantOwnerOrdersQuery(pageNumber, pageSize, status), cancellationToken);
        return result.ToHttpResult(HttpContext, page => Results.Ok(page));
    }

    /// <summary>Lấy chi tiết đơn khảo sát V3 và lịch bay hiện hành của tenant.</summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType<V3TenantOwnerSurveyOrderItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetOrder(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetV3TenantOwnerOrderQuery(orderId), cancellationToken);
        return result.ToHttpResult(HttpContext, order => Results.Ok(order));
    }

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

    /// <summary>Đọc metadata hóa đơn hiện hành tối giản dành cho Owner trước phát hành.</summary>
    [HttpGet("{orderId:guid}/invoice")]
    [ProducesResponseType<V3TenantOwnerInvoiceApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetInvoice(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetTenantOwnerSurveyOrderInvoiceQuery(orderId),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(V3SurveyResultAndInvoiceMapper.ToResponse(response)));
    }

    /// <summary>Khởi tạo checkout thanh toán số dư hóa đơn V3 bằng checkoutToken do backend cấp.</summary>
    [HttpPost("{orderId:guid}/balance-checkouts")]
    [ProducesResponseType<V3BalanceCheckoutApiResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<V3BalanceCheckoutApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> InitiateBalanceCheckout(
        [FromRoute] Guid orderId,
        [FromBody] InitiateV3BalanceCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new InitiateBalanceCheckoutCommand(orderId, request.CheckoutToken, request.IdempotencyKey),
            cancellationToken);
        return result.ToHttpResult(
            HttpContext,
            response => response.IsReplay
                ? Results.Ok(V3SurveyResultAndInvoiceMapper.ToResponse(response))
                : Results.Json(
                    V3SurveyResultAndInvoiceMapper.ToResponse(response),
                    statusCode: StatusCodes.Status201Created));
    }
}
