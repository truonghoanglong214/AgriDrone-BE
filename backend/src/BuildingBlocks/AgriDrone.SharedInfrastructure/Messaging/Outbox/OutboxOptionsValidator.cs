using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Messaging.Outbox;

internal sealed class OutboxOptionsValidator
    : IValidateOptions<OutboxOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        OutboxOptions options)
    {
        var failures = new List<string>();

        if (options.BatchSize is < 1 or > 1000)
        {
            failures.Add("Messaging:Outbox:BatchSize must be between 1 and 1000.");
        }

        if (options.PollIntervalMilliseconds <= 0)
        {
            failures.Add("Messaging:Outbox:PollIntervalMilliseconds must be greater than zero.");
        }

        if (options.LeaseSeconds <= 0)
        {
            failures.Add("Messaging:Outbox:LeaseSeconds must be greater than zero.");
        }

        if (options.MaximumAttempts <= 0)
        {
            failures.Add("Messaging:Outbox:MaximumAttempts must be greater than zero.");
        }

        if (options.RetryBaseSeconds <= 0 ||
            options.RetryMaximumSeconds < options.RetryBaseSeconds)
        {
            failures.Add(
                "The Outbox retry maximum must be greater than or equal to its positive base delay.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
