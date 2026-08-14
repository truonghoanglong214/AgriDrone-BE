using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Domain.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.FieldTasks.Infrastructure.Persistence.Configurations;

public sealed class TaskUpdateConfiguration : IEntityTypeConfiguration<TaskUpdate>
{
    public void Configure(EntityTypeBuilder<TaskUpdate> builder)
    {
        builder.ToTable(
            "task_updates",
            "field_task",
            tableBuilder => tableBuilder.HasComment(
                "Worker/manager progress and field result history for a task."));

        builder.HasKey(update => update.Id).HasName("pk_task_updates");

        builder.Property(update => update.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(update => update.TaskId)
            .HasColumnName("task_id")
            .HasColumnType("uuid");

        builder.Property(update => update.FarmId)
            .HasColumnName("farm_id")
            .HasColumnType("uuid");

        builder.Property(update => update.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(update => update.Result)
            .HasColumnName("result")
            .HasColumnType("system.task_result");

        builder.Property(update => update.Note)
            .HasColumnName("note")
            .HasColumnType("text");

        builder.Property(update => update.CreatedScanId)
            .HasColumnName("created_scan_id")
            .HasColumnType("uuid");

        builder.Property(update => update.ClientOperationId)
            .HasColumnName("client_operation_id")
            .HasColumnType("uuid");

        builder.Property(update => update.DeviceCreatedAt)
            .HasColumnName("device_created_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(update => update.ServerReceivedAt)
            .HasColumnName("server_received_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(update => update.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasIndex(update => new { update.TaskId, update.CreatedAt })
            .HasDatabaseName("ix_task_updates_task")
            .IsDescending(false, true);

        builder.HasIndex(update => update.ClientOperationId)
            .HasDatabaseName("uq_task_updates_client_operation")
            .HasFilter("client_operation_id IS NOT NULL")
            .IsUnique();

        builder.HasOne<FieldTask>()
            .WithMany()
            .HasForeignKey(update => new { update.TaskId, update.FarmId })
            .HasPrincipalKey(fieldTask => new { fieldTask.Id, fieldTask.FarmId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_task_updates_field_tasks_same_farm");
    }
}
