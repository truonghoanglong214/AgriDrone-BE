using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqOptionsValidator
    : IValidateOptions<RabbitMqOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        RabbitMqOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        Require(options.HostName, nameof(options.HostName), failures);
        Require(options.VirtualHost, nameof(options.VirtualHost), failures);
        Require(options.UserName, nameof(options.UserName), failures);
        Require(options.Password, nameof(options.Password), failures);
        Require(options.ConnectionName, nameof(options.ConnectionName), failures);
        Require(options.Exchange, nameof(options.Exchange), failures);
        Require(options.RetryExchange, nameof(options.RetryExchange), failures);
        Require(
            options.DeadLetterExchange,
            nameof(options.DeadLetterExchange),
            failures);

        if (options.Port is < 1 or > 65535)
        {
            failures.Add("RabbitMq:Port must be between 1 and 65535.");
        }

        if (options.PrefetchCount == 0)
        {
            failures.Add("RabbitMq:PrefetchCount must be greater than zero.");
        }

        if (options.InitialConnectionRetrySeconds <= 0 ||
            options.NetworkRecoverySeconds <= 0)
        {
            failures.Add("RabbitMQ retry intervals must be greater than zero.");
        }

        if (options.RetryDelaysSeconds.Length == 0 ||
            options.RetryDelaysSeconds.Any(delay => delay <= 0) ||
            !options.RetryDelaysSeconds.SequenceEqual(
                options.RetryDelaysSeconds.OrderBy(delay => delay)))
        {
            failures.Add(
                "RabbitMq:RetryDelaysSeconds must contain positive ascending values.");
        }

        foreach (var consumer in options.Consumers)
        {
            Require(consumer.Name, "Consumers:Name", failures);
            Require(consumer.QueueName, "Consumers:QueueName", failures);
            Require(consumer.RoutingKey, "Consumers:RoutingKey", failures);
        }

        AddDuplicateFailure(
            options.Consumers.Select(consumer => consumer.Name),
            "consumer name",
            failures);
        AddDuplicateFailure(
            options.Consumers.Select(consumer => consumer.QueueName),
            "queue name",
            failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void Require(
        string value,
        string field,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"RabbitMq:{field} is required.");
        }
    }

    private static void AddDuplicateFailure(
        IEnumerable<string> values,
        string field,
        List<string> failures)
    {
        if (values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            failures.Add($"RabbitMQ {field}s must be unique.");
        }
    }
}
