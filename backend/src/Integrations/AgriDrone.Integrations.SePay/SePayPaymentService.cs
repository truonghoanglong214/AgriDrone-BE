using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.SePay;

internal sealed class SePayPaymentService(
    IHttpClientFactory httpClientFactory,
    IOptions<SePayOptions> sePayOptions,
    IOptions<SurveyPaymentProviderOptions> paymentOptions)
    : ISurveyPaymentGateway, ISurveyPaymentCallbackVerifier
{
    private const string ProviderName = "sepay";
    private const string IpnSecretKeyScheme = "X-Secret-Key ";

    private readonly SePayOptions _sePayOptions = sePayOptions.Value;
    private readonly SurveyPaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public string Provider => ProviderName;

    public async Task<Result<PaymentSessionResult>> CreateSessionAsync(
        CreatePaymentSession request,
        CancellationToken cancellationToken = default)
    {
        if (!IsActive || request.Currency != "VND" ||
            request.Amount != decimal.Truncate(request.Amount))
        {
            return Result.Failure<PaymentSessionResult>(PaymentError.ProviderUnavailable());
        }

        var providerReference =
            $"{_sePayOptions.PaymentCodePrefix}{ToBase36(request.PaymentId)}"
                .ToUpperInvariant();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_amount"] = request.Amount.ToString("0", CultureInfo.InvariantCulture),
            ["merchant"] = _sePayOptions.MerchantId.Trim(),
            ["currency"] = "VND",
            ["operation"] = "PURCHASE",
            ["order_description"] = $"AgriDrone payment {request.OrderNumber}",
            ["order_invoice_number"] = providerReference,
            ["payment_method"] = "BANK_TRANSFER",
            ["success_url"] = _sePayOptions.SuccessUrl.Trim(),
            ["error_url"] = _sePayOptions.ErrorUrl.Trim(),
            ["cancel_url"] = _sePayOptions.CancelUrl.Trim()
        };
        fields["signature"] = CreateCheckoutSignature(fields, _sePayOptions.SecretKey);

        var checkoutEndpoint = _sePayOptions.Environment == "Production"
            ? "https://pay.sepay.vn/v1/checkout/init"
            : "https://pay-sandbox.sepay.vn/v1/checkout/init";
        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var response = await httpClientFactory.CreateClient("SePayCheckout")
                .PostAsync(checkoutEndpoint, content, cancellationToken);

            if ((int)response.StatusCode is < 300 or >= 400 ||
                response.Headers.Location is null)
            {
                return Result.Failure<PaymentSessionResult>(
                    PaymentError.ProviderUnavailable());
            }

            var redirectUrl = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(new Uri(checkoutEndpoint), response.Headers.Location);
            var expiresAt = request.RequestedAt.AddMinutes(
                _paymentOptions.SessionLifetimeMinutes);

            return Result.Success(new PaymentSessionResult(
                providerReference,
                redirectUrl,
                providerReference,
                expiresAt));
        }
        catch (HttpRequestException)
        {
            return Result.Failure<PaymentSessionResult>(
                PaymentError.ProviderUnavailable());
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<PaymentSessionResult>(
                PaymentError.ProviderUnavailable());
        }
    }

    public PaymentCallbackVerificationResult Verify(
        string provider,
        string rawPayload,
        string? timestamp,
        string? signature,
        string? authorization,
        DateTimeOffset receivedAt)
    {
        if (!IsActive || !string.Equals(
                provider.Trim(), ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.ProviderNotSupported");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawPayload);
        }
        catch (JsonException)
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("notification_type", out _))
            {
                return PaymentCallbackVerificationResult.Invalid(
                    "PaymentCallback.PayloadInvalid");
            }

            return VerifyPaymentGatewayIpn(root, rawPayload, authorization, receivedAt);
        }
    }

    private PaymentCallbackVerificationResult VerifyPaymentGatewayIpn(
        JsonElement root,
        string rawPayload,
        string? authorization,
        DateTimeOffset receivedAt)
    {
        if (!HasValidSecret(
                authorization,
                IpnSecretKeyScheme,
                _sePayOptions.IpnSecretKey))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.SecretInvalid");
        }

        var notificationType = GetString(root, "notification_type");
        if (!string.Equals(notificationType, "ORDER_PAID", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCallbackVerificationResult.Ignored();
        }

        if (!TryGetObject(root, "order", out var order) ||
            !TryGetObject(root, "transaction", out var transaction))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        var orderStatus = GetString(order, "order_status");
        var transactionStatus = GetString(transaction, "transaction_status");
        if (!string.Equals(orderStatus, "CAPTURED", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(transactionStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentCallbackVerificationResult.Ignored();
        }

        var providerReference = GetString(order, "order_invoice_number");
        var eventId = GetString(transaction, "id") ??
                      GetString(transaction, "transaction_id") ??
                      GetString(order, "id");
        var currency = GetString(transaction, "transaction_currency") ??
                       GetString(order, "order_currency");
        if (string.IsNullOrWhiteSpace(providerReference) ||
            string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(currency) ||
            !TryGetDecimal(transaction, "transaction_amount", out var amount) ||
            amount <= 0 ||
            !TryGetTransactionDate(transaction, root, out var occurredAt) ||
            occurredAt > receivedAt.AddMinutes(_paymentOptions.CallbackToleranceMinutes))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        try
        {
            _ = PaymentEventIdentity.Create(ProviderName, providerReference, eventId);
            return PaymentCallbackVerificationResult.Valid(
                new VerifiedSurveyPaymentEvent(
                    ProviderName,
                    providerReference.Trim(),
                    eventId.Trim(),
                    "payment.succeeded",
                    Money.Round(amount),
                    currency.Trim().ToUpperInvariant(),
                    occurredAt,
                    JsonDocument.Parse(rawPayload)));
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }
    }

    private bool IsActive =>
        _paymentOptions.Enabled &&
        string.Equals(_paymentOptions.Provider.Trim(), ProviderName,
            StringComparison.OrdinalIgnoreCase);

    private static string CreateCheckoutSignature(
        Dictionary<string, string> fields,
        string secretKey)
    {
        string[] signedFieldNames =
        [
            "order_amount", "merchant", "currency", "operation",
            "order_description", "order_invoice_number", "customer_id",
            "payment_method", "success_url", "error_url", "cancel_url"
        ];
        var signedString = string.Join(',', signedFieldNames
            .Where(fields.ContainsKey)
            .Select(name => $"{name}={fields[name]}"));
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secretKey),
            Encoding.UTF8.GetBytes(signedString));
        return Convert.ToBase64String(signature);
    }

    private static bool HasValidSecret(
        string? headerValue,
        string scheme,
        string expectedSecret)
    {
        if (string.IsNullOrWhiteSpace(headerValue) ||
            !headerValue.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var received = Encoding.UTF8.GetBytes(headerValue[scheme.Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(expectedSecret);
        return received.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(received, expected);
    }

    private static bool TryGetObject(
        JsonElement parent,
        string name,
        out JsonElement value) =>
        parent.TryGetProperty(name, out value) &&
        value.ValueKind == JsonValueKind.Object;

    private static string? GetString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static bool TryGetDecimal(
        JsonElement parent,
        string name,
        out decimal value)
    {
        value = default;
        var raw = GetString(parent, name);
        return decimal.TryParse(raw, NumberStyles.Number,
            CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetTransactionDate(
        JsonElement transaction,
        JsonElement root,
        out DateTimeOffset occurredAt)
    {
        if (TryParseTransactionDate(GetString(transaction, "transaction_date"),
                out occurredAt))
        {
            return true;
        }

        if (long.TryParse(GetString(root, "timestamp"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var timestamp))
        {
            try
            {
                occurredAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Return false below for invalid provider timestamps.
            }
        }

        occurredAt = default;
        return false;
    }

    private static bool TryParseTransactionDate(
        string? transactionDate,
        out DateTimeOffset occurredAt)
    {
        if (DateTime.TryParseExact(transactionDate, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var localDateTime))
        {
            occurredAt = new DateTimeOffset(
                DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified),
                TimeSpan.FromHours(7)).ToUniversalTime();
            return true;
        }

        occurredAt = default;
        return false;
    }

    private static string ToBase36(Guid value)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var number = new BigInteger(value.ToByteArray(), isUnsigned: true, isBigEndian: true);
        var characters = new StringBuilder();
        do
        {
            number = BigInteger.DivRem(number, 36, out var remainder);
            characters.Insert(0, alphabet[(int)remainder]);
        } while (number > BigInteger.Zero);

        return characters.ToString();
    }
}
