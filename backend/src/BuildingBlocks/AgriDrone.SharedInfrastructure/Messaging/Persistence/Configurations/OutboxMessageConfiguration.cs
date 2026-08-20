using AgriDrone.IntegrationContracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;

public sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(
            "outbox_messages",
            "system",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Integration events awaiting reliable delivery to the message broker.");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_schema_version",
                    "schema_version > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_attempt_count",
                    "attempt_count >= 0");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_body_size",
                    $"octet_length(body) BETWEEN 1 AND {IntegrationContractLimits.MaximumMessageBodyBytes}");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_status",
                    "status IN ('PENDING', 'PROCESSING', 'RETRY', 'PUBLISHED', 'DEAD')");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_processing_lease",
                    "(status = 'PROCESSING' AND locked_by IS NOT NULL AND locked_until IS NOT NULL) OR " +
                    "(status <> 'PROCESSING' AND locked_by IS NULL AND locked_until IS NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_publication",
                    "(status = 'PUBLISHED' AND published_at IS NOT NULL) OR " +
                    "(status <> 'PUBLISHED' AND published_at IS NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_retry_schedule",
                    "(status IN ('PENDING', 'RETRY') AND next_attempt_at IS NOT NULL) OR " +
                    "(status NOT IN ('PENDING', 'RETRY') AND next_attempt_at IS NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_outbox_messages_timestamps",
                    "created_at >= occurred_at AND " +
                    "(published_at IS NULL OR published_at >= created_at)");
            });

        builder.HasKey(message => message.MessageId)
            .HasName("pk_outbox_messages");

        builder.Property(message => message.MessageId)
            .HasColumnName("message_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(message => message.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(message => message.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(message => message.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("uuid");

        builder.Property(message => message.EventType)
            .HasColumnName("event_type")
            .HasColumnType(
                $"character varying({IntegrationContractLimits.MaximumEventTypeLength})")
            .HasMaxLength(IntegrationContractLimits.MaximumEventTypeLength)
            .IsRequired();

        builder.Property(message => message.SchemaVersion)
            .HasColumnName("schema_version")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(message => message.RoutingKey)
            .HasColumnName("routing_key")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumRoutingKeyLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumRoutingKeyLength)
            .IsRequired();

        builder.Property(message => message.Body)
            .HasColumnName("body")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(message => message.ContentType)
            .HasColumnName("content_type")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumContentTypeLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumContentTypeLength)
            .IsRequired();

        builder.Property(message => message.PartitionKey)
            .HasColumnName("partition_key")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumPartitionKeyLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumPartitionKeyLength);

        builder.Property(message => message.Status)
            .HasColumnName("status")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<OutboxMessageStatus>(value, true))
            .IsRequired();

        builder.Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(message => message.NextAttemptAt)
            .HasColumnName("next_attempt_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.LockedBy)
            .HasColumnName("locked_by")
            .HasColumnType("uuid");

        builder.Property(message => message.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.PublishedAt)
            .HasColumnName("published_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumErrorLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumErrorLength);

        builder.HasIndex(message => new
        {
            message.Status,
            message.NextAttemptAt,
            message.OccurredAt
        })
            .HasDatabaseName("ix_outbox_messages_dispatch")
            .HasFilter("status IN ('PENDING', 'RETRY')");

        builder.HasIndex(message => new
        {
            message.Status,
            message.LockedUntil
        })
            .HasDatabaseName("ix_outbox_messages_lease")
            .HasFilter("status = 'PROCESSING'");

        builder.HasIndex(message => new
        {
            message.TenantId,
            message.CorrelationId
        })
            .HasDatabaseName("ix_outbox_messages_tenant_correlation");

        builder.HasIndex(message => new
        {
            message.PartitionKey,
            message.OccurredAt
        })
            .HasDatabaseName("ix_outbox_messages_partition")
            .HasFilter("partition_key IS NOT NULL");
    }
}
