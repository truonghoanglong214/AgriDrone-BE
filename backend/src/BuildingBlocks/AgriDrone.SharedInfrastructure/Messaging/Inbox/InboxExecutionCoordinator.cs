using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AgriDrone.SharedInfrastructure.Messaging.Inbox;

public sealed class InboxExecutionCoordinator(TimeProvider timeProvider)
{
    public async Task<IntegrationMessageProcessingResult> ExecuteAsync<
        TDbContext,
        TPayload>(
        TDbContext dbContext,
        string consumerName,
        IntegrationEventEnvelope<TPayload> envelope,
        Func<TDbContext, CancellationToken, Task<InboxHandlerResult>> handler,
        CancellationToken cancellationToken = default)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(handler);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(
                "InboxExecutionCoordinator must own the business transaction.");
        }

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            var existing = await dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    message =>
                        message.ConsumerName == consumerName &&
                        message.MessageId == envelope.MessageId,
                    cancellationToken);

            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return FromExisting(existing);
            }

            var inboxMessage = InboxMessage.Start(
                consumerName,
                envelope,
                timeProvider.GetUtcNow());
            dbContext.Set<InboxMessage>().Add(inboxMessage);

            // Force the composite primary key check before invoking business code.
            await dbContext.SaveChangesAsync(cancellationToken);

            var handlerResult = await handler(
                dbContext,
                cancellationToken);
            switch (handlerResult.Disposition)
            {
                case InboxHandlerDisposition.Completed:
                    inboxMessage.Complete(
                        handlerResult.Result,
                        timeProvider.GetUtcNow());
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return IntegrationMessageProcessingResult.Acknowledge();

                case InboxHandlerDisposition.PermanentFailure:
                    if (string.IsNullOrWhiteSpace(handlerResult.ErrorCode))
                    {
                        throw new InvalidOperationException(
                            "A permanent Inbox failure requires an error code.");
                    }

                    inboxMessage.Fail(
                        handlerResult.ErrorCode,
                        handlerResult.Error,
                        timeProvider.GetUtcNow());
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return IntegrationMessageProcessingResult.DeadLetter(
                        handlerResult.ErrorCode,
                        handlerResult.Error);

                case InboxHandlerDisposition.Retry:
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return IntegrationMessageProcessingResult.Retry(
                        handlerResult.Error);

                default:
                    throw new InvalidOperationException(
                        $"Unknown Inbox handler disposition '{handlerResult.Disposition}'.");
            }
        }
        catch (DbUpdateException exception)
            when (IsInboxDuplicate(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            var existing = await dbContext.Set<InboxMessage>()
                .AsNoTracking()
                .SingleAsync(
                    message =>
                        message.ConsumerName == consumerName &&
                        message.MessageId == envelope.MessageId,
                    cancellationToken);
            return FromExisting(existing);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private static IntegrationMessageProcessingResult FromExisting(
        InboxMessage message) =>
        message.Status switch
        {
            InboxMessageStatus.Completed =>
                IntegrationMessageProcessingResult.Acknowledge(),
            InboxMessageStatus.Failed =>
                IntegrationMessageProcessingResult.DeadLetter(
                    message.ErrorCode ?? "INBOX_PREVIOUSLY_FAILED",
                    message.LastError),
            InboxMessageStatus.Processing =>
                IntegrationMessageProcessingResult.Retry(
                    "The Inbox message is already being processed."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(message),
                message.Status,
                "Unknown Inbox status.")
        };

    private static bool IsInboxDuplicate(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "pk_inbox_messages"
        };
}
