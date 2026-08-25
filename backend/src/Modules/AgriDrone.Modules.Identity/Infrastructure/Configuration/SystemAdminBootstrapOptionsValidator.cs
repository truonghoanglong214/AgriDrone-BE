using System.Net.Mail;
using AgriDrone.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Infrastructure.Configuration;

internal sealed class SystemAdminBootstrapOptionsValidator
    : IValidateOptions<SystemAdminBootstrapOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        SystemAdminBootstrapOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            errors.Add(
                $"{SystemAdminBootstrapOptions.SectionName}:Email is required.");
        }
        else if (options.Email.Trim().Length > 320)
        {
            errors.Add(
                $"{SystemAdminBootstrapOptions.SectionName}:Email must not exceed 320 characters.");
        }
        else if (!MailAddress.TryCreate(options.Email.Trim(), out var address) ||
                 !string.Equals(
                     address.Address,
                     options.Email.Trim(),
                     StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{SystemAdminBootstrapOptions.SectionName}:Email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(options.FullName))
        {
            errors.Add(
                $"{SystemAdminBootstrapOptions.SectionName}:FullName is required.");
        }
        else if (options.FullName.Trim().Length > 150)
        {
            errors.Add(
                $"{SystemAdminBootstrapOptions.SectionName}:FullName must not exceed 150 characters.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
