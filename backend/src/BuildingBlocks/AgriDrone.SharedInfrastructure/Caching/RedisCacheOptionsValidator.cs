using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Caching;

internal sealed class RedisCacheOptionsValidator
    : IValidateOptions<RedisCacheOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        RedisCacheOptions options)
    {
        var failures = new List<string>();

        if (options.Enabled &&
            string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add(
                "Redis:ConnectionString is required when Redis is enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.InstancePrefix))
        {
            failures.Add("Redis:InstancePrefix is required.");
        }

        if (options.PlantReferenceTtlSeconds <= 0)
        {
            failures.Add(
                "Redis:PlantReferenceTtlSeconds must be greater than zero.");
        }

        if (options.InvalidationEpochTtlSeconds <= 0)
        {
            failures.Add(
                "Redis:InvalidationEpochTtlSeconds must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
