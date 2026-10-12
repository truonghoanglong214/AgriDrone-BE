using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.SharedKernel.Application;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Surveys.Infrastructure.Payments;

internal sealed class SurveyPaymentGateway(
    IOptions<SurveyPaymentProviderOptions> options)
    : ISurveyPaymentGateway
{
    private readonly SurveyPaymentProviderOptions _options = options.Value;

    public string Provider => _options.Provider.Trim().ToLowerInvariant();

    public Task<Result<PaymentSessionResult>> CreateSessionAsync(
        CreatePaymentSession request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled)
        {
            return Task.FromResult(
                Result.Failure<PaymentSessionResult>(
                    PaymentError.ProviderUnavailable()));
        }

        var providerReference = $"pay_{Guid.CreateVersion7():N}";
        var expiresAt = request.RequestedAt.AddMinutes(
            _options.SessionLifetimeMinutes);
        var tokenPayload = string.Join(
            '\n',
            request.PaymentId.ToString("D"),
            providerReference,
            request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            request.Currency,
            expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        var clientToken = Sign(tokenPayload);
        var separator = _options.CheckoutBaseUrl.Contains('?', StringComparison.Ordinal)
            ? '&'
            : '?';
        var redirectUrl = new Uri(
            $"{_options.CheckoutBaseUrl}{separator}" +
            $"paymentId={Uri.EscapeDataString(request.PaymentId.ToString("D"))}&" +
            $"reference={Uri.EscapeDataString(providerReference)}&" +
            $"token={Uri.EscapeDataString(clientToken)}");

        return Task.FromResult(Result.Success(new PaymentSessionResult(
            providerReference,
            redirectUrl,
            clientToken,
            expiresAt)));
    }

    private string Sign(string value)
    {
        var secret = Encoding.UTF8.GetBytes(_options.SigningSecret);
        var payload = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(HMACSHA256.HashData(secret, payload))
            .ToLowerInvariant();
    }
}
