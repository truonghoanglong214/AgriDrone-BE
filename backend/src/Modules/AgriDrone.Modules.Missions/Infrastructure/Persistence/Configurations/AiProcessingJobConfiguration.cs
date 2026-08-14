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
                tableBuilder.HasCheckConstraint("ck_ai_job_attempt", "attempt_number >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_ai_job_progress",
                    "progress_percent IS NULL OR progress_percent BETWEEN 0 AND 100");
                tableBuilder.HasCheckConstraint(
                    "ck_ai_job_threshold_model",
                    "threshold_profile_id IS NULL OR model_version_id IS NOT NULL");
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

        builder.Property(job => job.ModelVersionId)
            .HasColumnName("model_version_id")
            .HasColumnType("uuid");

        builder.Property(job => job.ThresholdProfileId)
            .HasColumnName("threshold_profile_id")
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

        builder.Property(job => job.AttemptNumber)
            .HasColumnName("attempt_number")
            .HasColumnType("integer")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(job => job.ProgressPercent)
            .HasColumnName("progress_percent")
            .HasColumnType("numeric(5,2)")
            .HasPrecision(5, 2);

        builder.Property(job => job.InputManifest)
            .HasColumnName("input_manifest")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(job => job.OutputManifest)
            .HasColumnName("output_manifest")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(job => job.ErrorCode)
            .HasColumnName("error_code")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(job => job.ClientOperationId)
            .HasColumnName("client_operation_id")
            .HasColumnType("uuid");

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

        builder.HasIndex(job => job.ClientOperationId)
            .HasDatabaseName("uq_ai_jobs_client_operation")
            .HasFilter("client_operation_id IS NOT NULL")
            .IsUnique();

        builder.HasOne<DroneMission>()
            .WithMany()
            .HasForeignKey(job => job.MissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_ai_processing_jobs_drone_missions_mission_id");

        builder.HasOne<AiModelVersion>()
            .WithMany()
            .HasForeignKey(job => job.ModelVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ai_processing_jobs_model_versions_model_id");

        builder.HasOne<AiThresholdProfile>()
            .WithMany()
            .HasForeignKey(job => new { job.ThresholdProfileId, job.ModelVersionId })
            .HasPrincipalKey(profile => new { profile.Id, profile.ModelVersionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ai_processing_jobs_threshold_profile_same_model");
    }
}
