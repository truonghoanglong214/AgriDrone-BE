using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal sealed partial class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopologyReady topologyReady,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyInitializer> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            LogMessagingDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeclareTopologyAsync(stoppingToken);
                topologyReady.MarkReady();
                LogTopologyReady(
                    logger,
                    _options.HostName,
                    _options.Port,
                    _options.VirtualHost);
                return;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogTopologyFailure(
                    logger,
                    _options.InitialConnectionRetrySeconds,
                    exception);
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _options.InitialConnectionRetrySeconds),
                    stoppingToken);
            }
        }
    }

    private async Task DeclareTopologyAsync(
        CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(
            cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            exchange: _options.RetryExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            exchange: _options.DeadLetterExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        foreach (var consumer in _options.Consumers)
        {
            await DeclareConsumerTopologyAsync(
                channel,
                consumer,
                cancellationToken);
        }
    }

    private async Task DeclareConsumerTopologyAsync(
        IChannel channel,
        RabbitMqConsumerOptions consumer,
        CancellationToken cancellationToken)
    {
        var deadLetterRoutingKey =
            RabbitMqTopologyNames.DeadLetterRoutingKey(consumer);
        var mainArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = deadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(
            queue: consumer.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainArguments,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: consumer.QueueName,
            exchange: _options.Exchange,
            routingKey: consumer.RoutingKey,
            cancellationToken: cancellationToken);

        var deadLetterQueue =
            RabbitMqTopologyNames.DeadLetterQueue(consumer);
        await channel.QueueDeclareAsync(
            queue: deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: deadLetterQueue,
            exchange: _options.DeadLetterExchange,
            routingKey: deadLetterRoutingKey,
            cancellationToken: cancellationToken);

        for (var index = 0;
             index < _options.RetryDelaysSeconds.Length;
             index++)
        {
            var retryArguments = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = checked(
                    _options.RetryDelaysSeconds[index] * 1000),
                ["x-dead-letter-exchange"] = _options.Exchange,
                ["x-dead-letter-routing-key"] = consumer.RoutingKey
            };
            var retryQueue = RabbitMqTopologyNames.RetryQueue(
                consumer,
                index);
            var retryRoutingKey =
                RabbitMqTopologyNames.RetryRoutingKey(consumer, index);

            await channel.QueueDeclareAsync(
                queue: retryQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retryArguments,
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                queue: retryQueue,
                exchange: _options.RetryExchange,
                routingKey: retryRoutingKey,
                cancellationToken: cancellationToken);
        }
    }

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        Message = "RabbitMQ messaging is disabled by configuration.")]
    private static partial void LogMessagingDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Information,
        Message = "RabbitMQ topology is ready on {Host}:{Port}/{VirtualHost}.")]
    private static partial void LogTopologyReady(
        ILogger logger,
        string host,
        int port,
        string virtualHost);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Error,
        Message = "RabbitMQ topology initialization failed; retrying in {DelaySeconds} seconds.")]
    private static partial void LogTopologyFailure(
        ILogger logger,
        int delaySeconds,
        Exception exception);
}
