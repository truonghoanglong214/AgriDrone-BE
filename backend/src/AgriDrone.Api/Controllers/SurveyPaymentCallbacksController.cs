using System.Text;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;
using AgriDrone.SharedInfrastructure.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgriDrone.Api.Controllers;

[ApiController]
[Route("api/integrations/payments")]
[AllowAnonymous]
public sealed class SurveyPaymentCallbacksController(
    ISender sender,
    ISurveyPaymentCallbackVerifier callbackVerifier,
    IOptions<SurveyPaymentProviderOptions> options,
    TimeProvider timeProvider)
    : ControllerBase
{
    private readonly SurveyPaymentProviderOptions _options = options.Value;

    /// <summary>Nhận IPN xác nhận thanh toán từ SePay Payment Gateway.</summary>
    [HttpPost("sepay/ipn")]
    [Consumes("application/json")]
    [ProducesResponseType<SurveyPaymentCallbackResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<IResult> ProcessSePayIpn(
        [FromHeader(Name = "X-Secret-Key")] string? secretKey,
        CancellationToken cancellationToken) =>
        ProcessSePayIpnAsync(secretKey, cancellationToken);

    private async Task<IResult> ProcessSePayIpnAsync(
        string? secretKey,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength.HasValue &&
            Request.ContentLength.Value > _options.MaximumCallbackBytes)
        {
            return PayloadTooLarge();
        }

        await using var payloadBuffer = new MemoryStream();
        var buffer = new byte[8_192];
        while (true)
        {
            var bytesRead = await Request.Body.ReadAsync(
                buffer,
                cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (payloadBuffer.Length + bytesRead >
                _options.MaximumCallbackBytes)
            {
                return PayloadTooLarge();
            }

            await payloadBuffer.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        var rawPayload = Encoding.UTF8.GetString(
            payloadBuffer.GetBuffer(),
            0,
            checked((int)payloadBuffer.Length));
        var receivedAt = timeProvider.GetUtcNow();
        var verification = callbackVerifier.Verify(
            "sepay",
            rawPayload,
            timestamp: null,
            signature: null,
            authorization: secretKey is null ? null : $"X-Secret-Key {secretKey}",
            receivedAt: receivedAt);
        if (!verification.IsValid || verification.Event is null)
        {
            if (verification.IsValid)
            {
                return Results.Ok(new { success = true });
            }

            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid payment callback",
                extensions: new Dictionary<string, object?>
                {
                    ["errorCode"] = verification.FailureCode,
                    ["traceId"] = HttpContext.TraceIdentifier
                });
        }

        using var payload = verification.Event.Payload;
        var paymentEvent = verification.Event;
        var result = await sender.Send(
            new ProcessSurveyPaymentEventCommand(
                paymentEvent.Provider,
                paymentEvent.ProviderReference,
                paymentEvent.ProviderEventId,
                paymentEvent.EventType,
                paymentEvent.Amount,
                paymentEvent.Currency,
                paymentEvent.OccurredAt,
                receivedAt,
                payload),
            cancellationToken);
        if (result.IsFailure && result.Error.Code == PaymentError.NotFoundCode)
        {
            return Results.Ok(new SurveyPaymentCallbackResponse(true));
        }

        return result.ToHttpResult(
            HttpContext,
            _ => Results.Ok(new SurveyPaymentCallbackResponse(true)));
    }

    private IResult PayloadTooLarge() => Results.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Payment callback payload too large",
        extensions: new Dictionary<string, object?>
        {
            ["errorCode"] = "PaymentCallback.PayloadTooLarge",
            ["traceId"] = HttpContext.TraceIdentifier
        });
}
