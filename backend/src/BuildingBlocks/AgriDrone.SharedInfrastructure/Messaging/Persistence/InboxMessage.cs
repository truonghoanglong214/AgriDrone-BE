using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.SharedInfrastructure.Messaging.Persistence;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    private InboxMessage(
        string consumerName,
        Guid messageId,
        Guid tenantId,
        Guid correlationId,
        string eventType,
        int schemaVersion,
        DateTimeOffset receivedAt)
    {
        ConsumerName = consumerName;
        MessageId = messageId;
        TenantId = tenantId;
        CorrelationId = correlationId;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        Status = InboxMessageStatus.Processing;
        ReceivedAt = receivedAt;
    }

    public string ConsumerName { get; private set; } = null!;

    public Guid MessageId { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CorrelationId { get; private set; }

    public string EventType { get; private set; } = null!;

    public int SchemaVersion { get; private set; }

    public InboxMessageStatus Status { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? Result { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? LastError { get; private set; }

    public static InboxMessage Start<TPayload>(
        string consumerName,
        IntegrationEventEnvelope<TPayload> envelope,
        DateTimeOffset receivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureUtc(receivedAt, nameof(receivedAt));

        if (consumerName.Length >
            MessagingPersistenceLimits.MaximumConsumerNameLength)
        {
            throw new ArgumentException(
                $"Consumer name cannot exceed {MessagingPersistenceLimits.MaximumConsumerNameLength} characters.",
                nameof(consumerName));
        }

        return new InboxMessage(
            consumerName.Trim(),
            envelope.MessageId,
            envelope.TenantId,
            envelope.CorrelationId,
            envelope.EventType,
            envelope.SchemaVersion,
            receivedAt);
    }

    public void Complete(string? result, DateTimeOffset completedAt)
    {
        EnsureCanComplete(completedAt);

        Status = InboxMessageStatus.Completed;
        Result = result;
        ErrorCode = null;
        LastError = null;
        CompletedAt = completedAt;
    }

    public void Fail(
        string errorCode,
        string? error,
        DateTimeOffset completedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        EnsureCanComplete(completedAt);

        if (errorCode.Length >
            MessagingPersistenceLimits.MaximumErrorCodeLength)
        {
            throw new ArgumentException(
                $"Error code cannot exceed {MessagingPersistenceLimits.MaximumErrorCodeLength} characters.",
                nameof(errorCode));
        }

        Status = InboxMessageStatus.Failed;
        Result = null;
        ErrorCode = errorCode.Trim();
        LastError = Truncate(error);
        CompletedAt = completedAt;
    }

    private void EnsureCanComplete(DateTimeOffset completedAt)
    {
        if (Status != InboxMessageStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Inbox message in status '{Status}' cannot be completed again.");
        }

        EnsureUtc(completedAt, nameof(completedAt));

        if (completedAt < ReceivedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedAt),
                completedAt,
                "Completion time cannot be before receipt time.");
        }
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

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MessagingPersistenceLimits.MaximumErrorLength
            ? trimmed
            : trimmed[..MessagingPersistenceLimits.MaximumErrorLength];
    }
}
