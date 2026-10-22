using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.SePay;

internal sealed class SePayPaymentService(
    IOptions<SePayOptions> sePayOptions,
    IOptions<SurveyPaymentProviderOptions> paymentOptions)
    : ISurveyPaymentGateway, ISurveyPaymentCallbackVerifier
{
    private const string ProviderName = "sepay";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SePayOptions _sePayOptions = sePayOptions.Value;
    private readonly SurveyPaymentProviderOptions _paymentOptions = paymentOptions.Value;

    public string Provider => ProviderName;

    public Task<Result<PaymentSessionResult>> CreateSessionAsync(
        CreatePaymentSession request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsActive || request.Currency != "VND" ||
            request.Amount != decimal.Truncate(request.Amount))
        {
            return Task.FromResult(
                Result.Failure<PaymentSessionResult>(PaymentError.ProviderUnavailable()));
        }

        var providerReference =
            $"{_sePayOptions.PaymentCodePrefix}{ToBase36(request.PaymentId)}"
                .ToUpperInvariant();
        var expiresAt = request.RequestedAt.AddMinutes(
            _paymentOptions.SessionLifetimeMinutes);
        var query = string.Join('&',
            $"acc={Uri.EscapeDataString(_sePayOptions.BankAccountNumber.Trim())}",
            $"bank={Uri.EscapeDataString(_sePayOptions.BankShortName.Trim())}",
            $"amount={request.Amount.ToString("0", CultureInfo.InvariantCulture)}",
            $"des={Uri.EscapeDataString(providerReference)}",
            $"template={Uri.EscapeDataString(_sePayOptions.QrTemplate)}",
            "showinfo=true",
            $"holder={Uri.EscapeDataString(StripDiacritics(_sePayOptions.AccountHolder.Trim()))}");
        var qrUrl = new Uri($"https://vietqr.app/img?{query}");

        return Task.FromResult(Result.Success(new PaymentSessionResult(
            providerReference,
            qrUrl,
            providerReference,
            expiresAt)));
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

        if (!HasValidApiKey(authorization))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.ApiKeyInvalid");
        }

        SePayCallbackPayload? callback;
        try
        {
            callback = JsonSerializer.Deserialize<SePayCallbackPayload>(
                rawPayload, SerializerOptions);
        }
        catch (JsonException)
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        if (callback is null || callback.Id <= 0 ||
            string.IsNullOrWhiteSpace(callback.TransferType))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        var providerReference = FindProviderReference(
            callback.Code,
            callback.Content,
            _sePayOptions.PaymentCodePrefix);
        if (!string.Equals(callback.TransferType, "in", StringComparison.OrdinalIgnoreCase) ||
            providerReference is null ||
            !string.Equals(callback.AccountNumber?.Trim(),
                _sePayOptions.BankAccountNumber.Trim(), StringComparison.Ordinal))
        {
            return PaymentCallbackVerificationResult.Ignored();
        }

        if (callback.TransferAmount <= 0 ||
            !TryParseTransactionDate(callback.TransactionDate, out var occurredAt) ||
            occurredAt > receivedAt.AddMinutes(_paymentOptions.CallbackToleranceMinutes))
        {
            return PaymentCallbackVerificationResult.Invalid(
                "PaymentCallback.PayloadInvalid");
        }

        try
        {
            var reference = providerReference;
            var eventId = callback.Id.ToString(CultureInfo.InvariantCulture);
            _ = PaymentEventIdentity.Create(provider, reference, eventId);
            var payload = JsonDocument.Parse(rawPayload);
            return PaymentCallbackVerificationResult.Valid(
                new VerifiedSurveyPaymentEvent(
                    ProviderName,
                    reference,
                    eventId,
                    "payment.succeeded",
                    Money.Round(callback.TransferAmount),
                    "VND",
                    occurredAt,
                    payload));
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

    private bool HasValidApiKey(string? authorization)
    {
        const string scheme = "Apikey ";
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var received = Encoding.UTF8.GetBytes(authorization[scheme.Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(_sePayOptions.ApiKey);
        return received.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(received, expected);
    }

    private static string? FindProviderReference(
        string? code,
        string? content,
        string paymentCodePrefix)
    {
        var prefix = paymentCodePrefix.Trim();
        if (MatchesPaymentReference(code, prefix))
        {
            return code!.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var pattern = $"(?<![A-Za-z0-9]){Regex.Escape(prefix)}[A-Za-z0-9]+(?![A-Za-z0-9])";
        var matches = Regex.Matches(
                content,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Select(match => match.Value.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool MatchesPaymentReference(string? value, string prefix) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
        value.Trim().Length > prefix.Length &&
        value.Trim().All(char.IsAsciiLetterOrDigit);

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

    private static string StripDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD)
            .Replace('đ', 'd').Replace('Đ', 'D');
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                UnicodeCategory.NonSpacingMark)
            {
                result.Append(character);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record SePayCallbackPayload(
        long Id,
        string? TransactionDate,
        string? AccountNumber,
        string? Code,
        string? Content,
        string? TransferType,
        decimal TransferAmount);
}
