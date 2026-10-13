using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Surveys.Infrastructure.Payments;

internal sealed class SurveyPaymentProviderOptionsValidator
    : IValidateOptions<SurveyPaymentProviderOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        SurveyPaymentProviderOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Provider) ||
            options.Provider.Trim().Length > 50)
        {
            failures.Add("SurveyPayments:Provider is required and must not exceed 50 characters.");
        }

        if (options.Enabled &&
            (!Uri.TryCreate(
                 options.CheckoutBaseUrl,
                 UriKind.Absolute,
                 out var checkoutUri) ||
             checkoutUri.Scheme is not ("http" or "https")))
        {
            failures.Add("SurveyPayments:CheckoutBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (options.Enabled &&
            (string.IsNullOrEmpty(options.SigningSecret) ||
             options.SigningSecret.Length < 32))
        {
            failures.Add("SurveyPayments:SigningSecret must contain at least 32 characters when payments are enabled.");
        }

        if (options.SessionLifetimeMinutes is < 1 or > 60)
        {
            failures.Add("SurveyPayments:SessionLifetimeMinutes must be between 1 and 60.");
        }

        if (options.CallbackToleranceMinutes is < 1 or > 30)
        {
            failures.Add("SurveyPayments:CallbackToleranceMinutes must be between 1 and 30.");
        }

        if (options.MaximumCallbackBytes is < 1_024 or > 1_048_576)
        {
            failures.Add("SurveyPayments:MaximumCallbackBytes must be between 1024 and 1048576.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
