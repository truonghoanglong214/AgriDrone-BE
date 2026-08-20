using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using AgriDrone.SharedInfrastructure.Messaging.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AgriDrone.SharedInfrastructure.Messaging.Recovery;

internal sealed class MessagingRecoveryService(
    MessagingDbContext dbContext,
    IAuditWriter auditWriter,
    RabbitMqConnectionProvider connectionProvider,
    RabbitMqTopologyReady topologyReady,
    IRabbitMqPublisher publisher,
    IOptions<RabbitMqOptions> options,
    TimeProvider timeProvider) : IMessagingRecoveryService
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task<bool> RedriveOutboxAsync(
        Guid messageId,
        Guid actorId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(messageId, Guid.Empty);
        ValidateActor(actorId, correlationId);

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            candidate => candidate.MessageId == messageId,
            cancellationToken);
        if (message is null || message.Status != OutboxMessageStatus.Dead)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = OutboxMessageStatus.Dead.ToString(),
            message.AttemptCount,
            message.LastError
        });
        message.Redrive(timeProvider.GetUtcNow());
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = message.Status.ToString(),
            message.AttemptCount,
            message.NextAttemptAt
        });
        auditWriter.AddSystemAdminAction(
            dbContext,
            actorId,
            correlationId,
            "OutboxMessage",
            message.MessageId,
            "REDRIVE",
            oldData,
            newData,
            timeProvider.GetUtcNow());

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<int> RedriveDeadLettersAsync(
        string consumerName,
        int maximumMessages,
        Guid actorId,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumMessages, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumMessages, 100);
        ValidateActor(actorId, correlationId);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException(
                "RabbitMQ is disabled by configuration.");
        }

        var consumer = _options.GetConsumer(consumerName.Trim());
        await topologyReady.WaitAsync(cancellationToken);
        var connection = await connectionProvider.GetConnectionAsync(
            cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);
        var redriven = 0;

        while (redriven < maximumMessages)
        {
            var delivery = await channel.BasicGetAsync(
                RabbitMqTopologyNames.DeadLetterQueue(consumer),
                autoAck: false,
                cancellationToken);
            if (delivery is null)
            {
                break;
            }

            var properties = delivery.BasicProperties;
            var headers = properties.Headers is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(properties.Headers);
            var routingKey = ReadHeader(
                    headers,
                    RabbitMqHeaders.OriginalRoutingKey) ??
                consumer.RoutingKey;
            RemoveFailureHeaders(headers);
            headers["x-agridrone-redriven-by"] = actorId.ToString("D");
            headers["x-agridrone-redriven-at"] = timeProvider
                .GetUtcNow()
                .ToString("O");

            var messageId = properties.MessageId ??
                Guid.NewGuid().ToString("D");
            await WriteDeadLetterAuditAsync(
                messageId,
                consumer.Name,
                actorId,
                correlationId,
                "DLQ_REDRIVE_REQUESTED",
                cancellationToken);

            await publisher.PublishAsync(
                new RabbitMqPublishMessage(
                    _options.Exchange,
                    routingKey,
                    delivery.Body,
                    properties.ContentType ??
                        Outbox.OutboxMessageFactory.JsonContentType,
                    messageId,
                    properties.CorrelationId,
                    properties.Type,
                    headers),
                cancellationToken);
            await channel.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false,
                cancellationToken);
            await WriteDeadLetterAuditAsync(
                messageId,
                consumer.Name,
                actorId,
                correlationId,
                "DLQ_REDRIVE_COMPLETED",
                cancellationToken);
            redriven++;
        }

        return redriven;
    }

    private async Task WriteDeadLetterAuditAsync(
        string messageId,
        string consumerName,
        Guid actorId,
        Guid correlationId,
        string action,
        CancellationToken cancellationToken)
    {
        using var data = JsonSerializer.SerializeToDocument(new
        {
            MessageId = messageId,
            ConsumerName = consumerName
        });
        auditWriter.AddSystemAdminAction(
            dbContext,
            actorId,
            correlationId,
            "RabbitMqDeadLetter",
            ToAuditEntityId(messageId),
            action,
            oldData: null,
            data,
            timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
    }

    private static void RemoveFailureHeaders(
        Dictionary<string, object?> headers)
    {
        headers.Remove(RabbitMqHeaders.ErrorCode);
        headers.Remove(RabbitMqHeaders.Error);
        headers.Remove(RabbitMqHeaders.FailedAt);
        headers.Remove(RabbitMqHeaders.OriginalExchange);
        headers.Remove(RabbitMqHeaders.OriginalRoutingKey);
    }

    private static string? ReadHeader(
        Dictionary<string, object?> headers,
        string name)
    {
        if (!headers.TryGetValue(name, out var value))
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string text => text,
            _ => value?.ToString()
        };
    }

    private static Guid ToAuditEntityId(string messageId)
    {
        if (Guid.TryParse(messageId, out var id) && id != Guid.Empty)
        {
            return id;
        }

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(messageId), hash);
        return new Guid(hash[..16]);
    }

    private static void ValidateActor(Guid actorId, Guid correlationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(actorId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(
            correlationId,
            Guid.Empty);
    }
}
