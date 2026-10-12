using System.Text;
using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Api.Mapping;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;
using AgriDrone.Modules.Surveys.Infrastructure.Payments;
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
    private const string TimestampHeader = "X-AgriDrone-Payment-Timestamp";
    private const string SignatureHeader = "X-AgriDrone-Payment-Signature";
    private readonly SurveyPaymentProviderOptions _options = options.Value;

    /// <summary>Nhận callback thanh toán đã ký từ provider.</summary>
    [HttpPost("{provider}/callbacks")]
    [Consumes("application/json")]
    [ProducesResponseType<SurveyPaymentCallbackResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IResult> ProcessCallback(
        [FromRoute] string provider,
        [FromHeader(Name = TimestampHeader)] string? timestamp,
        [FromHeader(Name = SignatureHeader)] string? signature,
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
            provider,
            rawPayload,
            timestamp,
            signature,
            receivedAt);
        if (!verification.IsValid || verification.Event is null)
        {
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
        return result.ToHttpResult(
            HttpContext,
            response => Results.Ok(
                SurveyPaymentResponseMapper.ToResponse(response)));
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
