using Microsoft.Extensions.Options;

namespace AgriDrone.Integrations.Media;

internal sealed class MinioStorageOptionsValidator
    : IValidateOptions<MinioStorageOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        MinioStorageOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(
                options.Endpoint,
                UriKind.Absolute,
                out var endpoint) ||
            endpoint.Scheme is not "http" and not "https")
        {
            failures.Add(
                "ObjectStorage:Endpoint must be a valid HTTP or HTTPS URL.");
        }
        else if (options.UseSsl &&
                 endpoint.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add(
                "ObjectStorage:Endpoint must use HTTPS when UseSsl is true.");
        }

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            failures.Add("ObjectStorage:Bucket is required.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            failures.Add("ObjectStorage:AccessKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add("ObjectStorage:SecretKey is required.");
        }

        if (options.PresignedUrlExpiryMinutes is < 1 or > 60)
        {
            failures.Add(
                "ObjectStorage:PresignedUrlExpiryMinutes must be between 1 and 60.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}