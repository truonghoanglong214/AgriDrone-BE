using System.Data;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.SharedInfrastructure.Messaging.Outbox;

internal sealed class OutboxStore(MessagingDbContext dbContext)
{
    private const string ExpiredLeaseError =
        "The previous dispatcher lease expired before publication was recorded.";

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        Guid dispatcherId,
        int batchSize,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

        await dbContext.OutboxMessages
            .Where(message =>
                message.Status == OutboxMessageStatus.Processing &&
                message.LockedUntil <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        message => message.Status,
                        OutboxMessageStatus.Retry)
                    .SetProperty(message => message.NextAttemptAt, now)
                    .SetProperty(message => message.LockedBy, (Guid?)null)
                    .SetProperty(message => message.LockedUntil, (DateTimeOffset?)null)
                    .SetProperty(message => message.LastError, ExpiredLeaseError),
                cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($$"""
                SELECT *
                FROM system.outbox_messages
                WHERE status IN ('PENDING', 'RETRY')
                  AND next_attempt_at <= {{now}}
                ORDER BY occurred_at, message_id
                FOR UPDATE SKIP LOCKED
                LIMIT {{batchSize}}
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var lockedUntil = now.Add(leaseDuration);
        foreach (var message in messages)
        {
            message.MarkProcessing(dispatcherId, lockedUntil, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return messages;
    }

    public async Task<bool> MarkPublishedAsync(
        Guid messageId,
        Guid dispatcherId,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken)
    {
        var message = await FindOwnedMessageAsync(
            messageId,
            dispatcherId,
            cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.MarkPublished(dispatcherId, publishedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ScheduleRetryAsync(
        Guid messageId,
        Guid dispatcherId,
        DateTimeOffset failedAt,
        DateTimeOffset nextAttemptAt,
        string error,
        CancellationToken cancellationToken)
    {
        var message = await FindOwnedMessageAsync(
            messageId,
            dispatcherId,
            cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.ScheduleRetry(
            dispatcherId,
            failedAt,
            nextAttemptAt,
            error);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MarkDeadAsync(
        Guid messageId,
        Guid dispatcherId,
        string error,
        CancellationToken cancellationToken)
    {
        var message = await FindOwnedMessageAsync(
            messageId,
            dispatcherId,
            cancellationToken);
        if (message is null)
        {
            return false;
        }

        message.MarkDead(dispatcherId, error);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<OutboxMessage?> FindOwnedMessageAsync(
        Guid messageId,
        Guid dispatcherId,
        CancellationToken cancellationToken) =>
        dbContext.OutboxMessages.SingleOrDefaultAsync(
            message =>
                message.MessageId == messageId &&
                message.Status == OutboxMessageStatus.Processing &&
                message.LockedBy == dispatcherId,
            cancellationToken);
}
