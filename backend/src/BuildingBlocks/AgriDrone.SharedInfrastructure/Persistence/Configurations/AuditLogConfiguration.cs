using AgriDrone.SharedInfrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.SharedInfrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(
            "audit_logs",
            "system",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Append-only audit trail for user, AI and background-system actions.");
                tableBuilder.HasCheckConstraint(
                    "ck_audit_actor_context",
                    "(actor_type = 'USER'::system.audit_actor_type AND " +
                    "COALESCE(actor_id, user_id) IS NOT NULL) OR " +
                    "(actor_type = 'AI'::system.audit_actor_type AND source_job_id IS NOT NULL) OR " +
                    "actor_type = 'SYSTEM'::system.audit_actor_type");
                tableBuilder.HasCheckConstraint(
                    "ck_audit_farm_tenant_context",
                    "farm_id IS NULL OR tenant_id IS NOT NULL");
            });

        builder.HasKey(auditLog => auditLog.Id)
            .HasName("pk_audit_logs");

        builder.Property(auditLog => auditLog.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(auditLog => auditLog.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.ActorType)
            .HasColumnName("actor_type")
            .HasColumnType("system.audit_actor_type")
            .HasDefaultValueSql("'SYSTEM'::system.audit_actor_type")
            .IsRequired();

        builder.Property(auditLog => auditLog.ActorId)
            .HasColumnName("actor_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.SourceJobId)
            .HasColumnName("source_job_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.EntityType)
            .HasColumnName("entity_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditLog => auditLog.EntityId)
            .HasColumnName("entity_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.Action)
            .HasColumnName("action")
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(auditLog => auditLog.OldData)
            .HasColumnName("old_data")
            .HasColumnType("jsonb");

        builder.Property(auditLog => auditLog.NewData)
            .HasColumnName("new_data")
            .HasColumnType("jsonb");

        builder.Property(auditLog => auditLog.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(
                auditLog => new
                {
                    auditLog.EntityType,
                    auditLog.EntityId,
                    auditLog.CreatedAt
                })
            .HasDatabaseName("ix_audit_logs_entity")
            .IsDescending(false, false, true);

        builder.HasIndex(auditLog => new { auditLog.FarmId, auditLog.CreatedAt })
            .HasDatabaseName("ix_audit_logs_farm")
            .IsDescending(false, true);

        builder.HasIndex(auditLog => new { auditLog.TenantId, auditLog.CreatedAt })
            .HasDatabaseName("ix_audit_logs_tenant")
            .IsDescending(false, true);

        builder.HasIndex(auditLog => auditLog.CorrelationId)
            .HasDatabaseName("ix_audit_logs_correlation");

        builder.HasIndex(auditLog => auditLog.SourceJobId)
            .HasDatabaseName("ix_audit_logs_source_job");
    }
}
