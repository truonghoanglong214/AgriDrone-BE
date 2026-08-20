using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Messaging.Retention;

internal sealed class MessagingRetentionOptionsValidator
    : IValidateOptions<MessagingRetentionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        MessagingRetentionOptions options)
    {
        var failures = new List<string>();

        if (options.CleanupIntervalHours <= 0)
        {
            failures.Add(
                "Messaging:Retention:CleanupIntervalHours must be greater than zero.");
        }

        if (options.CompletedInboxRetentionDays <= 0)
        {
            failures.Add(
                "Messaging:Retention:CompletedInboxRetentionDays must be greater than zero.");
        }

        if (options.PublishedOutboxRetentionDays <= 0)
        {
            failures.Add(
                "Messaging:Retention:PublishedOutboxRetentionDays must be greater than zero.");
        }

        if (options.BatchSize is < 1 or > 10_000)
        {
            failures.Add(
                "Messaging:Retention:BatchSize must be between 1 and 10000.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
