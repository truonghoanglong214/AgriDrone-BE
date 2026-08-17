using AgriDrone.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Infrastructure.Configuration;

internal sealed class PasswordResetOptionsValidator
    : IValidateOptions<PasswordResetOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        PasswordResetOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.ResetUrl, UriKind.Absolute, out var resetUri) ||
            (resetUri.Scheme != Uri.UriSchemeHttp &&
             resetUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add(
                $"{PasswordResetOptions.SectionName}:ResetUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (options.ExpirationMinutes is < 5 or > 1440)
        {
            failures.Add(
                $"{PasswordResetOptions.SectionName}:ExpirationMinutes must be between 5 and 1440.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
