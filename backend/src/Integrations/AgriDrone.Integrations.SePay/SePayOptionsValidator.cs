using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.SePay;

internal sealed class SePayOptionsValidator(IConfiguration configuration)
    : IValidateOptions<SePayOptions>
{
    public ValidateOptionsResult Validate(string? name, SePayOptions options)
    {
        var isEnabled = configuration.GetValue<bool>("SurveyPayments:Enabled");
        var isSelected = string.Equals(
            configuration["SurveyPayments:Provider"]?.Trim(),
            "sepay",
            StringComparison.OrdinalIgnoreCase);
        if (!isEnabled || !isSelected)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (options.Environment is not ("Sandbox" or "Production"))
        {
            failures.Add("SePay:Environment must be Sandbox or Production.");
        }

        if (string.IsNullOrWhiteSpace(options.MerchantId) || options.MerchantId.Length > 150)
        {
            failures.Add("SePay:MerchantId is required and must not exceed 150 characters when SePay payments are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey) || options.SecretKey.Length > 1000)
        {
            failures.Add("SePay:SecretKey is required and must not exceed 1000 characters when SePay payments are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.IpnSecretKey) || options.IpnSecretKey.Length > 1000)
        {
            failures.Add("SePay:IpnSecretKey is required and must not exceed 1000 characters when SePay payments are enabled.");
        }

        ValidatePublicUrl(options.SuccessUrl, "SePay:SuccessUrl", failures);
        ValidatePublicUrl(options.ErrorUrl, "SePay:ErrorUrl", failures);
        ValidatePublicUrl(options.CancelUrl, "SePay:CancelUrl", failures);

        if (string.IsNullOrWhiteSpace(options.PaymentCodePrefix) ||
            options.PaymentCodePrefix.Length is < 2 or > 5 ||
            !options.PaymentCodePrefix.All(char.IsAsciiLetterOrDigit))
        {
            failures.Add("SePay:PaymentCodePrefix must contain 2-5 ASCII letters or digits.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidatePublicUrl(
        string value,
        string key,
        List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{key} must be an absolute HTTPS URL.");
        }
    }
}
