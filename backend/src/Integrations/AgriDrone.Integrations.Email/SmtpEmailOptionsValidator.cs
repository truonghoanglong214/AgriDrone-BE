using Microsoft.Extensions.Options;
using MimeKit;

namespace AgriDrone.Integrations.Email;

internal sealed class SmtpEmailOptionsValidator : IValidateOptions<SmtpEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, SmtpEmailOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add($"{SmtpEmailOptions.SectionName}:Host is required when SMTP email is enabled.");
        }

        if (options.Port is < 1 or > 65_535)
        {
            failures.Add($"{SmtpEmailOptions.SectionName}:Port must be between 1 and 65535.");
        }

        if (options.TimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"{SmtpEmailOptions.SectionName}:TimeoutSeconds must be between 1 and 300.");
        }

        if (!MailboxAddress.TryParse(options.FromAddress, out _))
        {
            failures.Add($"{SmtpEmailOptions.SectionName}:FromAddress must be a valid email address.");
        }

        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);

        if (hasUsername != hasPassword)
        {
            failures.Add($"{SmtpEmailOptions.SectionName}:Username and Password must either both be provided or both be omitted.");
        }

        if (hasUsername && options.SecurityMode is
            SmtpSecurityMode.Auto or
            SmtpSecurityMode.None or
            SmtpSecurityMode.StartTlsWhenAvailable)
        {
            failures.Add(
                $"{SmtpEmailOptions.SectionName}:SecurityMode must be StartTls or SslOnConnect when SMTP credentials are configured.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
