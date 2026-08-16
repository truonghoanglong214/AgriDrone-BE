using AgriDrone.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Infrastructure.Configuration;

internal sealed class TenantInvitationOptionsValidator
    : IValidateOptions<TenantInvitationOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        TenantInvitationOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.AcceptUrl, UriKind.Absolute, out var acceptUri) ||
            (acceptUri.Scheme != Uri.UriSchemeHttp &&
             acceptUri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add(
                $"{TenantInvitationOptions.SectionName}:AcceptUrl must be an absolute HTTP or HTTPS URL.");
        }

        if (options.ExpirationHours is < 1 or > 168)
        {
            failures.Add(
                $"{TenantInvitationOptions.SectionName}:ExpirationHours must be between 1 and 168.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
