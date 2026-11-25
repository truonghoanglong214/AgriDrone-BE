using AgriDrone.Modules.Identity.Application.Options;
using Microsoft.Extensions.Options;

namespace AgriDrone.Modules.Identity.Infrastructure.Configuration;

internal sealed class RefreshSessionOptionsValidator : IValidateOptions<RefreshSessionOptions>
{
    public ValidateOptionsResult Validate(string? name, RefreshSessionOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.CookieName))
        {
            failures.Add($"{RefreshSessionOptions.SectionName}:CookieName must not be empty.");
        }

        if (options.IdleLifetimeDays <= 0)
        {
            failures.Add($"{RefreshSessionOptions.SectionName}:IdleLifetimeDays must be greater than zero.");
        }

        if (options.AbsoluteLifetimeDays <= 0)
        {
            failures.Add($"{RefreshSessionOptions.SectionName}:AbsoluteLifetimeDays must be greater than zero.");
        }

        if (options.IdleLifetimeDays > 0 &&
            options.AbsoluteLifetimeDays > 0 &&
            options.IdleLifetimeDays > options.AbsoluteLifetimeDays)
        {
            failures.Add($"{RefreshSessionOptions.SectionName}:IdleLifetimeDays must be less than or equal to AbsoluteLifetimeDays.");
        }

        if (options.AllowedOrigins is not null)
        {
            foreach (var rawOrigin in options.AllowedOrigins)
            {
                if (string.IsNullOrWhiteSpace(rawOrigin) ||
                    !Uri.TryCreate(rawOrigin.Trim(), UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                    !string.IsNullOrEmpty(uri.PathAndQuery.TrimEnd('/')))
                {
                    failures.Add($"{RefreshSessionOptions.SectionName}:AllowedOrigins contains an invalid origin '{rawOrigin}'.");
                }
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
