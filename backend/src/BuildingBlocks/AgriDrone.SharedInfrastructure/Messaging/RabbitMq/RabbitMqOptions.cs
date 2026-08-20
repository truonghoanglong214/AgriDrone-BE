namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConnectionName { get; set; } = "agridrone";

    public string Exchange { get; set; } = "agridrone.integration";

    public string RetryExchange { get; set; } = "agridrone.integration.retry";

    public string DeadLetterExchange { get; set; } = "agridrone.integration.dlx";

    public ushort PrefetchCount { get; set; } = 8;

    public int InitialConnectionRetrySeconds { get; set; } = 5;

    public int NetworkRecoverySeconds { get; set; } = 5;

    public int[] RetryDelaysSeconds { get; set; } = [];

    public List<RabbitMqConsumerOptions> Consumers { get; set; } = [];

    internal RabbitMqConsumerOptions GetConsumer(string consumerName) =>
        Consumers.Single(consumer =>
            string.Equals(
                consumer.Name,
                consumerName,
                StringComparison.Ordinal));
}
