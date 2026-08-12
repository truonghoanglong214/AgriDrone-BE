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
            tableBuilder => tableBuilder.HasComment(
                "Append-only audit trail for sensitive business changes and traceability."));

        builder.HasKey(auditLog => auditLog.Id)
            .HasName("pk_audit_logs");

        builder.Property(auditLog => auditLog.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .ValueGeneratedOnAdd();

        builder.Property(auditLog => auditLog.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(auditLog => auditLog.FarmId)
            .HasColumnName("farm_id")
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
    }
}
