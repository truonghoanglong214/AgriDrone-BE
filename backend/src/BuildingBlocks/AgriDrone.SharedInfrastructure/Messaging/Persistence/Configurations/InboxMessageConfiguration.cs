using AgriDrone.IntegrationContracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.SharedInfrastructure.Messaging.Persistence.Configurations;

public sealed class InboxMessageConfiguration
    : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable(
            "inbox_messages",
            "system",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Idempotency records and replay results for integration-event consumers.");
                tableBuilder.HasCheckConstraint(
                    "ck_inbox_messages_schema_version",
                    "schema_version > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_inbox_messages_status",
                    "status IN ('PROCESSING', 'COMPLETED', 'FAILED')");
                tableBuilder.HasCheckConstraint(
                    "ck_inbox_messages_completion",
                    "(status = 'PROCESSING' AND completed_at IS NULL) OR " +
                    "(status IN ('COMPLETED', 'FAILED') AND completed_at IS NOT NULL AND completed_at >= received_at)");
                tableBuilder.HasCheckConstraint(
                    "ck_inbox_messages_result",
                    "result IS NULL OR status = 'COMPLETED'");
                tableBuilder.HasCheckConstraint(
                    "ck_inbox_messages_error",
                    "(status = 'FAILED' AND error_code IS NOT NULL) OR " +
                    "(status <> 'FAILED' AND error_code IS NULL AND last_error IS NULL)");
            });

        builder.HasKey(message => new
        {
            message.ConsumerName,
            message.MessageId
        })
            .HasName("pk_inbox_messages");

        builder.Property(message => message.ConsumerName)
            .HasColumnName("consumer_name")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumConsumerNameLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumConsumerNameLength)
            .IsRequired();

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

        builder.Property(message => message.Status)
            .HasColumnName("status")
            .HasColumnType("character varying(30)")
            .HasMaxLength(30)
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                value => Enum.Parse<InboxMessageStatus>(value, true))
            .IsRequired();

        builder.Property(message => message.ReceivedAt)
            .HasColumnName("received_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.Result)
            .HasColumnName("result")
            .HasColumnType("jsonb");

        builder.Property(message => message.ErrorCode)
            .HasColumnName("error_code")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumErrorCodeLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumErrorCodeLength);

        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasColumnType(
                $"character varying({MessagingPersistenceLimits.MaximumErrorLength})")
            .HasMaxLength(MessagingPersistenceLimits.MaximumErrorLength);

        builder.HasIndex(message => new
        {
            message.Status,
            message.ReceivedAt
        })
            .HasDatabaseName("ix_inbox_messages_status_received_at");

        builder.HasIndex(message => new
        {
            message.TenantId,
            message.CorrelationId
        })
            .HasDatabaseName("ix_inbox_messages_tenant_correlation");
    }
}
