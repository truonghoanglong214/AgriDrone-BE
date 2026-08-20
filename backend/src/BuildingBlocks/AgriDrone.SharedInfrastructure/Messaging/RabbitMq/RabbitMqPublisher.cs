using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AgriDrone.SharedInfrastructure.Messaging.RabbitMq;

internal sealed class RabbitMqPublisher(
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopologyReady topologyReady,
    IOptions<RabbitMqOptions> options) : IRabbitMqPublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task PublishAsync(
        RabbitMqPublishMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException(
                "RabbitMQ publishing is disabled by configuration.");
        }

        await topologyReady.WaitAsync(cancellationToken);
        var connection = await connectionProvider.GetConnectionAsync(
            cancellationToken);
        var channelOptions = new CreateChannelOptions(true, true);
        await using var channel = await connection.CreateChannelAsync(
            channelOptions,
            cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = message.ContentType,
            MessageId = message.MessageId,
            CorrelationId = message.CorrelationId,
            Type = message.EventType,
            Timestamp = new AmqpTimestamp(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = message.Headers is null
                ? null
                : new Dictionary<string, object?>(message.Headers)
        };

        await channel.BasicPublishAsync(
            exchange: message.Exchange,
            routingKey: message.RoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: message.Body,
            cancellationToken: cancellationToken);
    }
}
