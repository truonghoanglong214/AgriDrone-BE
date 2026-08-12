using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence.Configurations;

public sealed class AiProcessingJobConfiguration : IEntityTypeConfiguration<AiProcessingJob>
{
    public void Configure(EntityTypeBuilder<AiProcessingJob> builder)
    {
        builder.ToTable(
            "ai_processing_jobs",
            "mission",
            tableBuilder =>
            {
                tableBuilder.HasComment(
                    "One AI processing execution for a mission. Keeps retries and failures instead of overwriting mission history.");
                tableBuilder.HasCheckConstraint(
                    "ck_ai_job_time",
                    "completed_at IS NULL OR started_at IS NULL OR completed_at >= started_at");
            });

        builder.HasKey(job => job.Id).HasName("pk_ai_processing_jobs");

        builder.Property(job => job.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();

        builder.Property(job => job.MissionId)
            .HasColumnName("mission_id")
            .HasColumnType("uuid");

        builder.Property(job => job.JobType)
            .HasColumnName("job_type")
            .HasColumnType("system.ai_job_type")
            .IsRequired();

        builder.Property(job => job.Status)
            .HasColumnName("status")
            .HasColumnType("system.ai_job_status")
            .HasDefaultValueSql("'QUEUED'::system.ai_job_status")
            .IsRequired();

        builder.Property(job => job.ExternalJobId)
            .HasColumnName("external_job_id")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(job => job.Parameters)
            .HasColumnName("parameters")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(job => job.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(job => job.QueuedAt)
            .HasColumnName("queued_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(job => job.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(job => job.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(job => new { job.MissionId, job.QueuedAt })
            .HasDatabaseName("ix_ai_jobs_mission")
            .IsDescending(false, true);

        builder.HasIndex(job => job.Status)
            .HasDatabaseName("ix_ai_jobs_status");

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(job => job.MissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ai_processing_jobs_drone_missions_mission_id");
    }
}
