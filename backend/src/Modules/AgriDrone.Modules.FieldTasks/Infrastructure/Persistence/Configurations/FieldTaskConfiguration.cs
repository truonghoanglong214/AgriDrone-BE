using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.FieldTasks.Infrastructure.Persistence.Configurations;

public sealed class FieldTaskConfiguration : IEntityTypeConfiguration<FieldTask>
{
    public void Configure(EntityTypeBuilder<FieldTask> builder)
    {
        builder.ToTable(
            "field_tasks",
            "field_task",
            tableBuilder => tableBuilder.HasComment(
                "Field work created by managers, often originating from an AI scan that needs human verification."));

        builder.HasKey(fieldTask => fieldTask.Id).HasName("pk_field_tasks");
        builder.HasAlternateKey(fieldTask => new { fieldTask.Id, fieldTask.FarmId })
            .HasName("uq_field_tasks_id_farm");

        builder.Property(fieldTask => fieldTask.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(fieldTask => fieldTask.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(fieldTask => fieldTask.PlantId)
            .HasColumnName("plant_id")
            .HasColumnType("uuid");

        builder.Property(fieldTask => fieldTask.SourceScanId)
            .HasColumnName("source_scan_id")
            .HasColumnType("uuid");

        builder.Property(fieldTask => fieldTask.TaskType)
            .HasColumnName("task_type")
            .HasColumnType("system.task_type")
            .HasDefaultValueSql("'GENERAL'::system.task_type")
            .IsRequired();

        builder.Property(fieldTask => fieldTask.Title)
            .HasColumnName("title")
            .HasColumnType("character varying(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(fieldTask => fieldTask.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(fieldTask => fieldTask.Priority)
            .HasColumnName("priority")
            .HasColumnType("system.task_priority")
            .HasDefaultValueSql("'MEDIUM'::system.task_priority")
            .IsRequired();

        builder.Property(fieldTask => fieldTask.Status)
            .HasColumnName("status")
            .HasColumnType("system.task_status")
            .HasDefaultValueSql("'OPEN'::system.task_status")
            .IsRequired();

        builder.Property(fieldTask => fieldTask.DueAt)
            .HasColumnName("due_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(fieldTask => fieldTask.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(fieldTask => fieldTask.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(fieldTask => fieldTask.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(fieldTask => fieldTask.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(fieldTask => fieldTask.Version)
            .IsRowVersion();

        builder.HasIndex(fieldTask => new
        {
            fieldTask.FarmId,
            fieldTask.Status,
            fieldTask.DueAt
        })
            .HasDatabaseName("ix_tasks_farm_status");

        builder.HasIndex(fieldTask => fieldTask.PlantId)
            .HasDatabaseName("ix_tasks_plant");
    }
}
