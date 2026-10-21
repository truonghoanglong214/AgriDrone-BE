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
        if (string.IsNullOrWhiteSpace(options.ApiKey) || options.ApiKey.Length > 1000)
        {
            failures.Add("SePay:ApiKey is required and must not exceed 1000 characters when SePay payments are enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.BankAccountNumber) ||
            options.BankAccountNumber.Length > 50)
        {
            failures.Add("SePay:BankAccountNumber is required and must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.BankShortName) ||
            options.BankShortName.Length > 50)
        {
            failures.Add("SePay:BankShortName is required and must not exceed 50 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.AccountHolder) ||
            options.AccountHolder.Length > 100)
        {
            failures.Add("SePay:AccountHolder is required and must not exceed 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(options.PaymentCodePrefix) ||
            options.PaymentCodePrefix.Length is < 2 or > 5 ||
            !options.PaymentCodePrefix.All(char.IsAsciiLetterOrDigit))
        {
            failures.Add("SePay:PaymentCodePrefix must contain 2-5 ASCII letters or digits.");
        }

        if (options.QrTemplate is not ("compact" or "qronly" or "standee"))
        {
            failures.Add("SePay:QrTemplate must be compact, qronly or standee.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
