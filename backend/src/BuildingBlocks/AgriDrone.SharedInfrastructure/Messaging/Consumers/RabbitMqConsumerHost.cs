using AgriDrone.SharedInfrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AgriDrone.SharedInfrastructure.Messaging.Consumers;

internal sealed partial class RabbitMqConsumerHost(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IntegrationConsumerRegistration> registrations,
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopologyReady topologyReady,
    IRabbitMqPublisher publisher,
    TimeProvider timeProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConsumerHost> logger) : BackgroundService
{
    private const string RetriesExhaustedErrorCode =
        "MESSAGING_RETRIES_EXHAUSTED";
    private const int MaximumDeadLetterErrorLength = 2_048;

    private readonly RabbitMqOptions _options = options.Value;
    private readonly IntegrationConsumerRegistration[]
        _registrations = registrations.ToArray();

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (_registrations.Length == 0)
        {
            LogNoProcessors(logger);
            return;
        }

        await topologyReady.WaitAsync(stoppingToken);
        var connection = await connectionProvider.GetConnectionAsync(
            stoppingToken);
        var channels = new List<IChannel>(_registrations.Length);

        try
        {
            foreach (var registration in _registrations)
            {
                var consumerOptions = _options.GetConsumer(
                    registration.ConsumerName);
                var channel = await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);
                channels.Add(channel);

                await channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: _options.PrefetchCount,
                    global: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, eventArgs) =>
                    HandleDeliveryAsync(
                        channel,
                        registration,
                        consumerOptions,
                        eventArgs,
                        stoppingToken);

                await channel.BasicConsumeAsync(
                    queue: consumerOptions.QueueName,
                    autoAck: false,
                    consumerTag: registration.ConsumerName,
                    noLocal: false,
                    exclusive: false,
                    arguments: null,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                LogConsumerStarted(
                    logger,
                    registration.ConsumerName,
                    consumerOptions.QueueName);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            foreach (var channel in channels)
            {
                await channel.DisposeAsync();
            }
        }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        IntegrationConsumerRegistration registration,
        RabbitMqConsumerOptions consumerOptions,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        // RabbitMQ.Client owns the delivery buffer after this callback returns.
        var body = eventArgs.Body.ToArray();

        IntegrationMessageProcessingResult result;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = (IIntegrationMessageProcessor)scope
                .ServiceProvider
                .GetRequiredService(registration.ProcessorType);
            result = await processor.ProcessAsync(body, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            LogUnhandledProcessorFailure(
                logger,
                registration.ConsumerName,
                exception);
            result = IntegrationMessageProcessingResult.Retry(
                exception.Message);
        }

        switch (result.Disposition)
        {
            case IntegrationMessageDisposition.Acknowledge:
                await channel.BasicAckAsync(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    cancellationToken);
                break;

            case IntegrationMessageDisposition.DeadLetter:
                LogPermanentRejection(
                    logger,
                    registration.ConsumerName,
                    eventArgs.BasicProperties.MessageId,
                    result.ErrorCode,
                    result.Error);
                await DeadLetterAsync(
                    channel,
                    consumerOptions,
                    eventArgs,
                    body,
                    result.ErrorCode ?? "MESSAGING_PERMANENT_FAILURE",
                    result.Error,
                    cancellationToken);
                break;

            case IntegrationMessageDisposition.Retry:
                await RetryOrDeadLetterAsync(
                    channel,
                    consumerOptions,
                    eventArgs,
                    body,
                    result.Error,
                    cancellationToken);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown message disposition '{result.Disposition}'.");
        }
    }

    private async Task RetryOrDeadLetterAsync(
        IChannel channel,
        RabbitMqConsumerOptions consumerOptions,
        BasicDeliverEventArgs eventArgs,
        byte[] body,
        string? error,
        CancellationToken cancellationToken)
    {
        var retryCount = ReadRetryCount(
            eventArgs.BasicProperties.Headers);
        if (retryCount >= _options.RetryDelaysSeconds.Length)
        {
            LogRetriesExhausted(
                logger,
                eventArgs.BasicProperties.MessageId,
                retryCount,
                consumerOptions.QueueName,
                error);
            await DeadLetterAsync(
                channel,
                consumerOptions,
                eventArgs,
                body,
                RetriesExhaustedErrorCode,
                error,
                cancellationToken);
            return;
        }

        var headers = eventArgs.BasicProperties.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(
                eventArgs.BasicProperties.Headers);
        headers[RabbitMqHeaders.RetryCount] = retryCount + 1;

        try
        {
            await publisher.PublishAsync(
                new RabbitMqPublishMessage(
                    _options.RetryExchange,
                    RabbitMqTopologyNames.RetryRoutingKey(
                        consumerOptions,
                        retryCount),
                    body,
                    eventArgs.BasicProperties.ContentType ??
                        Outbox.OutboxMessageFactory.JsonContentType,
                    eventArgs.BasicProperties.MessageId ?? Guid.NewGuid().ToString("D"),
                    eventArgs.BasicProperties.CorrelationId,
                    eventArgs.BasicProperties.Type,
                    headers),
                cancellationToken);

            // ACK only after the retry copy has received a publisher confirm.
            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken);
        }
        catch (Exception exception)
        {
            LogRetryPublishFailure(
                logger,
                eventArgs.BasicProperties.MessageId,
                exception);
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken);
        }
    }

    private async Task DeadLetterAsync(
        IChannel channel,
        RabbitMqConsumerOptions consumerOptions,
        BasicDeliverEventArgs eventArgs,
        byte[] body,
        string errorCode,
        string? error,
        CancellationToken cancellationToken)
    {
        var headers = eventArgs.BasicProperties.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(
                eventArgs.BasicProperties.Headers);
        headers[RabbitMqHeaders.ErrorCode] = errorCode;
        headers[RabbitMqHeaders.Error] = TruncateError(error);
        headers[RabbitMqHeaders.FailedAt] = timeProvider
            .GetUtcNow()
            .ToString("O");
        headers[RabbitMqHeaders.OriginalExchange] = eventArgs.Exchange;
        headers[RabbitMqHeaders.OriginalRoutingKey] = eventArgs.RoutingKey;

        try
        {
            await publisher.PublishAsync(
                new RabbitMqPublishMessage(
                    _options.DeadLetterExchange,
                    RabbitMqTopologyNames.DeadLetterRoutingKey(
                        consumerOptions),
                    body,
                    eventArgs.BasicProperties.ContentType ??
                        Outbox.OutboxMessageFactory.JsonContentType,
                    eventArgs.BasicProperties.MessageId ??
                        Guid.NewGuid().ToString("D"),
                    eventArgs.BasicProperties.CorrelationId,
                    eventArgs.BasicProperties.Type,
                    headers),
                cancellationToken);

            // The original delivery is acknowledged only after the DLQ copy
            // has received a publisher confirm.
            await channel.BasicAckAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                cancellationToken);
        }
        catch (Exception exception)
        {
            LogDeadLetterPublishFailure(
                logger,
                eventArgs.BasicProperties.MessageId,
                exception);
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken);
        }
    }

    private static string TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "No error detail was provided.";
        }

        var trimmed = error.Trim();
        return trimmed.Length <= MaximumDeadLetterErrorLength
            ? trimmed
            : trimmed[..MaximumDeadLetterErrorLength];
    }

    private static int ReadRetryCount(
        IDictionary<string, object?>? headers)
    {
        if (headers is null ||
            !headers.TryGetValue(RabbitMqHeaders.RetryCount, out var value))
        {
            return 0;
        }

        return value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number when number is >= 0 and <= int.MaxValue => (int)number,
            _ => 0
        };
    }

    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Information,
        Message = "No integration message processors are registered; RabbitMQ queues will remain durable until their use cases are implemented.")]
    private static partial void LogNoProcessors(ILogger logger);

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Information,
        Message = "RabbitMQ consumer {ConsumerName} is listening on {QueueName} with manual acknowledgements.")]
    private static partial void LogConsumerStarted(
        ILogger logger,
        string consumerName,
        string queueName);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Error,
        Message = "Unhandled failure in consumer {ConsumerName}; the message will enter the retry flow.")]
    private static partial void LogUnhandledProcessorFailure(
        ILogger logger,
        string consumerName,
        Exception exception);

    [LoggerMessage(
        EventId = 303,
        Level = LogLevel.Warning,
        Message = "Consumer {ConsumerName} rejected message {MessageId} permanently. ErrorCode={ErrorCode}; Error={Error}.")]
    private static partial void LogPermanentRejection(
        ILogger logger,
        string consumerName,
        string? messageId,
        string? errorCode,
        string? error);

    [LoggerMessage(
        EventId = 304,
        Level = LogLevel.Error,
        Message = "Message {MessageId} exhausted {RetryCount} retries in queue {QueueName} and is moving to the DLQ. LastError={Error}.")]
    private static partial void LogRetriesExhausted(
        ILogger logger,
        string? messageId,
        int retryCount,
        string queueName,
        string? error);

    [LoggerMessage(
        EventId = 305,
        Level = LogLevel.Error,
        Message = "Could not publish retry copy for message {MessageId}; requeueing the original delivery.")]
    private static partial void LogRetryPublishFailure(
        ILogger logger,
        string? messageId,
        Exception exception);

    [LoggerMessage(
        EventId = 306,
        Level = LogLevel.Error,
        Message = "Could not publish message {MessageId} to the dead-letter exchange; requeueing the original delivery.")]
    private static partial void LogDeadLetterPublishFailure(
        ILogger logger,
        string? messageId,
        Exception exception);
}
