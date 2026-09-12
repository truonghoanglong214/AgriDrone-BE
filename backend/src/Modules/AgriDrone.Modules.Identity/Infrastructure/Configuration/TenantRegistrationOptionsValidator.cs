using AgriDrone.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Infrastructure.Configuration;

internal sealed class TenantRegistrationOptionsValidator
    : IValidateOptions<TenantRegistrationOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        TenantRegistrationOptions options)
    {
        if (Uri.TryCreate(
                options.LoginUrl,
                UriKind.Absolute,
                out var loginUri) &&
            (loginUri.Scheme == Uri.UriSchemeHttp ||
             loginUri.Scheme == Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{TenantRegistrationOptions.SectionName}:LoginUrl must be an absolute HTTP or HTTPS URL.");
    }
}
