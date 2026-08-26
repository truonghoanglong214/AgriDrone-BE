namespace AgriDrone.SharedInfrastructure.Messaging.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid messageId,
        Guid tenantId,
        Guid correlationId,
        Guid? actorId,
        string eventType,
        int schemaVersion,
        string routingKey,
        byte[] body,
        string contentType,
        string? partitionKey,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        MessageId = messageId;
        TenantId = tenantId;
        CorrelationId = correlationId;
        ActorId = actorId;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        RoutingKey = routingKey;
        Body = body;
        ContentType = contentType;
        PartitionKey = partitionKey;
        Status = OutboxMessageStatus.Pending;
        NextAttemptAt = createdAt;
        OccurredAt = occurredAt;
        CreatedAt = createdAt;
    }

    public Guid MessageId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CorrelationId { get; private set; }

    public Guid? ActorId { get; private set; }

    public string EventType { get; private set; } = null!;

    public int SchemaVersion { get; private set; }

    public string RoutingKey { get; private set; } = null!;

    public byte[] Body { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public string? PartitionKey { get; private set; }

    public OutboxMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public Guid? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public string? LastError { get; private set; }

    public uint Version { get; private set; }

    internal static OutboxMessage Create(
        Guid messageId,
        Guid tenantId,
        Guid correlationId,
        Guid? actorId,
        string eventType,
        int schemaVersion,
        string routingKey,
        byte[] body,
        string contentType,
        string? partitionKey,
        DateTimeOffset occurredAt,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (body.Length == 0)
        {
            throw new ArgumentException(
                "Serialized message body cannot be empty.",
                nameof(body));
        }

        return new OutboxMessage(
            messageId,
            tenantId,
            correlationId,
            actorId,
            eventType,
            schemaVersion,
            routingKey,
            body.ToArray(),
            contentType,
            partitionKey,
            occurredAt,
            createdAt);
    }

    public void MarkProcessing(
        Guid dispatcherId,
        DateTimeOffset lockedUntil,
        DateTimeOffset now)
    {
        if (Status is not OutboxMessageStatus.Pending and
            not OutboxMessageStatus.Retry)
        {
            throw new InvalidOperationException(
                $"Outbox message in status '{Status}' cannot be claimed.");
        }

        ArgumentOutOfRangeException.ThrowIfEqual(dispatcherId, Guid.Empty);
        EnsureUtc(now, nameof(now));
        EnsureUtc(lockedUntil, nameof(lockedUntil));

        if (NextAttemptAt > now)
        {
            throw new InvalidOperationException(
                "Outbox message is not ready for another delivery attempt.");
        }

        if (lockedUntil <= now)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lockedUntil),
                lockedUntil,
                "Lease expiration must be later than the claim time.");
        }

        Status = OutboxMessageStatus.Processing;
        AttemptCount++;
        NextAttemptAt = null;
        LockedBy = dispatcherId;
        LockedUntil = lockedUntil;
        LastError = null;
    }

    public void MarkPublished(Guid dispatcherId, DateTimeOffset publishedAt)
    {
        EnsureOwnedProcessingLease(dispatcherId);
        EnsureUtc(publishedAt, nameof(publishedAt));

        if (publishedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishedAt),
                publishedAt,
                "Publication time cannot be before creation time.");
        }

        Status = OutboxMessageStatus.Published;
        PublishedAt = publishedAt;
        ClearDeliverySchedule();
        LastError = null;
    }

    public void ScheduleRetry(
        Guid dispatcherId,
        DateTimeOffset failedAt,
        DateTimeOffset nextAttemptAt,
        string error)
    {
        EnsureOwnedProcessingLease(dispatcherId);
        EnsureUtc(failedAt, nameof(failedAt));
        EnsureUtc(nextAttemptAt, nameof(nextAttemptAt));

        if (failedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failedAt),
                failedAt,
                "Failure time cannot be before creation time.");
        }

        if (nextAttemptAt <= failedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptAt),
                nextAttemptAt,
                "Next attempt time must be later than the failure time.");
        }

        Status = OutboxMessageStatus.Retry;
        NextAttemptAt = nextAttemptAt;
        LockedBy = null;
        LockedUntil = null;
        LastError = RequireAndTruncateError(error);
    }

    public void MarkDead(Guid dispatcherId, string error)
    {
        EnsureOwnedProcessingLease(dispatcherId);

        Status = OutboxMessageStatus.Dead;
        ClearDeliverySchedule();
        LastError = RequireAndTruncateError(error);
    }

    public void Redrive(DateTimeOffset requestedAt)
    {
        if (Status != OutboxMessageStatus.Dead)
        {
            throw new InvalidOperationException(
                $"Only a DEAD outbox message can be redriven; current status is '{Status}'.");
        }

        EnsureUtc(requestedAt, nameof(requestedAt));
        Status = OutboxMessageStatus.Retry;
        AttemptCount = 0;
        NextAttemptAt = requestedAt;
        PublishedAt = null;
        LockedBy = null;
        LockedUntil = null;
        LastError = null;
    }

    private void EnsureOwnedProcessingLease(Guid dispatcherId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dispatcherId, Guid.Empty);

        if (Status != OutboxMessageStatus.Processing ||
            LockedBy != dispatcherId)
        {
            throw new InvalidOperationException(
                "Only the dispatcher owning the active processing lease can change this outbox message.");
        }
    }

    private void ClearDeliverySchedule()
    {
        NextAttemptAt = null;
        LockedBy = null;
        LockedUntil = null;
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timestamp must be a non-default UTC value.",
                parameterName);
        }
    }

    private static string RequireAndTruncateError(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        var trimmed = error.Trim();
        return trimmed.Length <= MessagingPersistenceLimits.MaximumErrorLength
            ? trimmed
            : trimmed[..MessagingPersistenceLimits.MaximumErrorLength];
    }
}
