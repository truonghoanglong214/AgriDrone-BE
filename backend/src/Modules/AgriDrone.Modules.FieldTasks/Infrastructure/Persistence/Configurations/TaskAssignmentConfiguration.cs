using AgriDrone.Modules.FieldTasks.Domain.Assignments;
using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.FieldTasks.Infrastructure.Persistence.Configurations;

public sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable(
            "task_assignments",
            "field_task",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "Assignment history for tasks; supports reassignment and multiple workers if needed.");
                tableBuilder.HasCheckConstraint(
                    "ck_assignment_time",
                    "unassigned_at IS NULL OR unassigned_at >= assigned_at");
            });

        builder.HasKey(assignment => assignment.Id).HasName("pk_task_assignments");

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(assignment => assignment.TaskId)
            .HasColumnName("task_id")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.UserId)
            .HasColumnName("user_id")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.AssignedBy)
            .HasColumnName("assigned_by")
            .HasColumnType("uuid");

        builder.Property(assignment => assignment.AssignedAt)
            .HasColumnName("assigned_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(assignment => assignment.UnassignedAt)
            .HasColumnName("unassigned_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(assignment => new { assignment.TaskId, assignment.UserId })
            .HasDatabaseName("ux_task_active_assignment_per_user")
            .HasFilter("unassigned_at IS NULL")
            .IsUnique();

        builder.HasIndex(assignment => new { assignment.UserId, assignment.AssignedAt })
            .HasDatabaseName("ix_task_assignments_user_active")
            .HasFilter("unassigned_at IS NULL")
            .IsDescending(false, true);

        builder.HasOne(assignment => assignment.Task)
            .WithMany(task => task.Assignments)
            .HasForeignKey(assignment => assignment.TaskId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_task_assignments_field_tasks_task_id");
    }
}
