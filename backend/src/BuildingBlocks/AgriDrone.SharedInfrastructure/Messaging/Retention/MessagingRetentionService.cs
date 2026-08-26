using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgriDrone.SharedInfrastructure.Messaging.Retention;

internal sealed partial class MessagingRetentionService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<MessagingRetentionOptions> options,
    ILogger<MessagingRetentionService> logger) : BackgroundService
{
    private readonly MessagingRetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogCleanupFailure(logger, exception);
            }

            await Task.Delay(
                TimeSpan.FromHours(_options.CleanupIntervalHours),
                timeProvider,
                stoppingToken);
        }
    }

    internal async Task<(int InboxDeleted, int OutboxDeleted)> CleanupAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<MessagingDbContext>();
        var now = timeProvider.GetUtcNow();
        var inboxCutoff = now.AddDays(
            -_options.CompletedInboxRetentionDays);
        var outboxCutoff = now.AddDays(
            -_options.PublishedOutboxRetentionDays);

        var inboxDeleted = await dbContext.InboxMessages
            .Where(message =>
                (message.Status == InboxMessageStatus.Completed ||
                 message.Status == InboxMessageStatus.Failed) &&
                message.CompletedAt < inboxCutoff)
            .OrderBy(message => message.CompletedAt)
            .Take(_options.BatchSize)
            .ExecuteDeleteAsync(cancellationToken);

        var outboxDeleted = await dbContext.OutboxMessages
            .Where(message =>
                message.Status == OutboxMessageStatus.Published &&
                message.PublishedAt < outboxCutoff)
            .OrderBy(message => message.PublishedAt)
            .Take(_options.BatchSize)
            .ExecuteDeleteAsync(cancellationToken);

        LogCleanupCompleted(logger, inboxDeleted, outboxDeleted);
        return (inboxDeleted, outboxDeleted);
    }

    [LoggerMessage(
        EventId = 600,
        Level = LogLevel.Information,
        Message = "Messaging retention removed {InboxDeleted} Inbox and {OutboxDeleted} Outbox records.")]
    private static partial void LogCleanupCompleted(
        ILogger logger,
        int inboxDeleted,
        int outboxDeleted);

    [LoggerMessage(
        EventId = 601,
        Level = LogLevel.Error,
        Message = "Messaging retention cleanup failed; it will retry on the next interval.")]
    private static partial void LogCleanupFailure(
        ILogger logger,
        Exception exception);
}
