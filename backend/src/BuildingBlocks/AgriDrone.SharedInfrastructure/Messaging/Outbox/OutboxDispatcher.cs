using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Messaging.Outbox;

internal sealed partial class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IRabbitMqPublisher publisher,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> outboxOptions,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private readonly OutboxOptions _outboxOptions = outboxOptions.Value;
    private readonly RabbitMqOptions _rabbitMqOptions = rabbitMqOptions.Value;
    private readonly Guid _dispatcherId = Guid.NewGuid();

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_outboxOptions.Enabled || !_rabbitMqOptions.Enabled)
        {
            LogDispatcherDisabled(
                logger,
                _outboxOptions.Enabled,
                _rabbitMqOptions.Enabled);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await ClaimBatchAsync(stoppingToken);
                if (messages.Count == 0)
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            _outboxOptions.PollIntervalMilliseconds),
                        stoppingToken);
                    continue;
                }

                foreach (var message in messages)
                {
                    await DispatchAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogPollingFailure(logger, exception);
                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        _outboxOptions.PollIntervalMilliseconds),
                    stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<OutboxStore>();
        return await store.ClaimAsync(
            _dispatcherId,
            _outboxOptions.BatchSize,
            timeProvider.GetUtcNow(),
            TimeSpan.FromSeconds(_outboxOptions.LeaseSeconds),
            cancellationToken);
    }

    private async Task DispatchAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var headers = new Dictionary<string, object?>
            {
                ["x-tenant-id"] = message.TenantId.ToString("D"),
                ["x-schema-version"] = message.SchemaVersion
            };

            if (message.ActorId.HasValue)
            {
                headers["x-actor-id"] =
                    message.ActorId.Value.ToString("D");
            }

            await publisher.PublishAsync(
                new RabbitMqPublishMessage(
                    _rabbitMqOptions.Exchange,
                    message.RoutingKey,
                    message.Body,
                    message.ContentType,
                    message.MessageId.ToString("D"),
                    message.CorrelationId.ToString("D"),
                    message.EventType,
                    headers),
                cancellationToken);

            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<OutboxStore>();
            var updated = await store.MarkPublishedAsync(
                message.MessageId,
                _dispatcherId,
                timeProvider.GetUtcNow(),
                cancellationToken);

            if (!updated)
            {
                LogLostLeaseAfterConfirm(logger, message.MessageId);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(
                message,
                exception,
                cancellationToken);
        }
    }

    private async Task RecordFailureAsync(
        OutboxMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAt = timeProvider.GetUtcNow();
        var error = exception.ToString();
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<OutboxStore>();

        if (message.AttemptCount >= _outboxOptions.MaximumAttempts)
        {
            await store.MarkDeadAsync(
                message.MessageId,
                _dispatcherId,
                error,
                cancellationToken);
            LogMessageDead(
                logger,
                message.MessageId,
                message.AttemptCount,
                exception);
            return;
        }

        var delaySeconds = CalculateRetryDelaySeconds(
            message.AttemptCount);
        await store.ScheduleRetryAsync(
            message.MessageId,
            _dispatcherId,
            failedAt,
            failedAt.AddSeconds(delaySeconds),
            error,
            cancellationToken);
        LogMessageRetry(
            logger,
            message.MessageId,
            message.AttemptCount,
            delaySeconds,
            exception);
    }

    private int CalculateRetryDelaySeconds(int attemptCount)
    {
        var exponent = Math.Min(Math.Max(attemptCount - 1, 0), 30);
        var delay = (long)_outboxOptions.RetryBaseSeconds << exponent;
        return (int)Math.Min(delay, _outboxOptions.RetryMaximumSeconds);
    }

    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "Outbox dispatcher is disabled. OutboxEnabled={OutboxEnabled}, RabbitMqEnabled={RabbitMqEnabled}.")]
    private static partial void LogDispatcherDisabled(
        ILogger logger,
        bool outboxEnabled,
        bool rabbitMqEnabled);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Error,
        Message = "Outbox polling failed; the dispatcher will retry.")]
    private static partial void LogPollingFailure(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} was confirmed by RabbitMQ, but its dispatcher lease was no longer owned. A duplicate delivery is possible.")]
    private static partial void LogLostLeaseAfterConfirm(
        ILogger logger,
        Guid messageId);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Error,
        Message = "Outbox message {MessageId} became DEAD after {AttemptCount} attempts.")]
    private static partial void LogMessageDead(
        ILogger logger,
        Guid messageId,
        int attemptCount,
        Exception exception);

    [LoggerMessage(
        EventId = 204,
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} failed on attempt {AttemptCount}; retrying in {DelaySeconds} seconds.")]
    private static partial void LogMessageRetry(
        ILogger logger,
        Guid messageId,
        int attemptCount,
        int delaySeconds,
        Exception exception);
}
