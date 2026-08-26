using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.SharedInfrastructure.Messaging.Outbox;

internal sealed class OutboxStore(MessagingDbContext dbContext)
{
    private const int MaximumClaimAttempts = 3;

    private const string ExpiredLeaseError =
        "The previous dispatcher lease expired before publication was recorded.";

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        Guid dispatcherId,
        int batchSize,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
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

        var lockedUntil = now.Add(leaseDuration);
        for (var attempt = 1; attempt <= MaximumClaimAttempts; attempt++)
        {
            var messages = await dbContext.OutboxMessages
                .Where(message =>
                    (message.Status == OutboxMessageStatus.Pending ||
                     message.Status == OutboxMessageStatus.Retry) &&
                    message.NextAttemptAt <= now)
                .OrderBy(message => message.OccurredAt)
                .ThenBy(message => message.MessageId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                return messages;
            }

            foreach (var message in messages)
            {
                message.MarkProcessing(dispatcherId, lockedUntil, now);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return messages;
            }
            catch (DbUpdateConcurrencyException)
                when (attempt < MaximumClaimAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new DbUpdateConcurrencyException(
            "Outbox messages could not be claimed after repeated concurrent updates.");
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
