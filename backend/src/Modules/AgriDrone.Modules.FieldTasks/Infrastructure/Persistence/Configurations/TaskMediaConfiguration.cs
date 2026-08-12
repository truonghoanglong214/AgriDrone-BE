using AgriDrone.Modules.FieldTasks.Domain.FieldTasks;
using AgriDrone.Modules.FieldTasks.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.FieldTasks.Infrastructure.Persistence.Configurations;

public sealed class TaskMediaConfiguration : IEntityTypeConfiguration<TaskMedia>
{
    public void Configure(EntityTypeBuilder<TaskMedia> builder)
    {
        builder.ToTable(
            "task_media",
            "field_task",
            tableBuilder => tableBuilder.HasComment(
                "Field photos or other evidence uploaded during task execution."));

        builder.HasKey(media => new { media.TaskId, media.MediaId })
            .HasName("pk_task_media");

        builder.Property(media => media.TaskId)
            .HasColumnName("task_id")
            .HasColumnType("uuid");

        builder.Property(media => media.MediaId)
            .HasColumnName("media_id")
            .HasColumnType("uuid");

        builder.Property(media => media.UploadedBy)
            .HasColumnName("uploaded_by")
            .HasColumnType("uuid");

        builder.Property(media => media.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.HasOne<FieldTask>()
            .WithMany()
            .HasForeignKey(media => media.TaskId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_task_media_field_tasks_task_id");
    }
}
