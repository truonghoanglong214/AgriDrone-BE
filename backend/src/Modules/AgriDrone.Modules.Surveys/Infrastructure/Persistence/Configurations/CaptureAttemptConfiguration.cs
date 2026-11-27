using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CaptureAttemptConfiguration : IEntityTypeConfiguration<CaptureAttempt>
{
    public void Configure(EntityTypeBuilder<CaptureAttempt> builder)
    {
        builder.ToTable(
            "capture_attempts",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_capture_attempts_index", "attempt_index >= 1");
                table.HasCheckConstraint("ck_capture_attempts_version", "version >= 1");
                table.HasCheckConstraint("ck_capture_attempts_updated_at", "updated_at >= created_at");
                table.HasCheckConstraint(
                    "ck_capture_attempts_flight_time",
                    "ended_at IS NULL OR started_at IS NULL OR ended_at >= started_at");
                table.HasCheckConstraint(
                    "ck_capture_attempts_successor_approval",
                    "(superseded_attempt_id IS NULL AND reflight_approved_by IS NULL AND " +
                    "reflight_approval_reason IS NULL AND reflight_approved_at IS NULL) OR " +
                    "(superseded_attempt_id IS NOT NULL AND reflight_approved_by IS NOT NULL AND " +
                    "reflight_approval_reason IS NOT NULL AND reflight_approved_at IS NOT NULL)");
            });

        builder.HasKey(attempt => attempt.Id).HasName("pk_capture_attempts");
        builder.HasAlternateKey(attempt => new
        {
            attempt.Id,
            attempt.TenantId,
            attempt.FarmId,
            attempt.OrderId
        }).HasName("uq_capture_attempts_id_owner");

        builder.Property(attempt => attempt.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(attempt => attempt.OrderId).HasColumnName("order_id").HasColumnType("uuid").IsRequired();
        builder.Property(attempt => attempt.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(attempt => attempt.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(attempt => attempt.AttemptIndex).HasColumnName("attempt_index").HasColumnType("integer").IsRequired();
        builder.Property(attempt => attempt.Status).HasColumnName("status").HasColumnType("system.capture_attempt_status").IsRequired();
        builder.Property(attempt => attempt.Version).HasColumnName("version").HasColumnType("bigint").IsConcurrencyToken().IsRequired();

        // ADR 0020: mission.drone_missions.capture_attempt_id is the canonical physical binding.
        builder.Ignore(attempt => attempt.MissionId);
        builder.Property(attempt => attempt.ParentMissionId).HasColumnName("parent_mission_id").HasColumnType("uuid");
        builder.Property(attempt => attempt.MappingSourceAttemptId).HasColumnName("mapping_source_attempt_id").HasColumnType("uuid");
        builder.Property(attempt => attempt.SupersededAttemptId).HasColumnName("superseded_attempt_id").HasColumnType("uuid");
        builder.Property(attempt => attempt.ReflightApprovedBy).HasColumnName("reflight_approved_by").HasColumnType("uuid");
        builder.Property(attempt => attempt.ReflightApprovalReason).HasColumnName("reflight_approval_reason").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.ReflightApprovedAt).HasColumnName("reflight_approved_at").HasColumnType("timestamp with time zone");
        builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(attempt => attempt.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(attempt => attempt.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone");
        builder.Property(attempt => attempt.EndedAt).HasColumnName("ended_at").HasColumnType("timestamp with time zone");
        builder.Property(attempt => attempt.PreflightEvidenceReference).HasColumnName("preflight_evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.FlightCompletionReceipt).HasColumnName("flight_completion_receipt").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.FlightFailureEvidence).HasColumnName("flight_failure_evidence").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.UploadManifestReference).HasColumnName("upload_manifest_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.UploadManifestFingerprint).HasColumnName("upload_manifest_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128);
        builder.Property(attempt => attempt.InputApprovalReference).HasColumnName("input_approval_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.ProcessingJobReference).HasColumnName("processing_job_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.ProcessingAcceptanceReceipt).HasColumnName("processing_acceptance_receipt").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.ReflightDecisionEvidence).HasColumnName("reflight_decision_evidence").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.CancellationReason).HasColumnName("cancellation_reason").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.CancellationEvidenceReference).HasColumnName("cancellation_evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(attempt => attempt.LastPredecessorHistoryId).HasColumnName("last_predecessor_history_id").HasColumnType("uuid").IsRequired();

        builder.HasIndex(attempt => new { attempt.OrderId, attempt.AttemptIndex })
            .HasDatabaseName("uq_capture_attempts_order_index")
            .IsUnique();
        builder.HasIndex(attempt => new { attempt.OrderId, attempt.TenantId, attempt.FarmId })
            .HasDatabaseName("ix_capture_attempts_order_owner");
        builder.HasIndex(attempt => attempt.ReflightApprovedBy)
            .HasDatabaseName("ix_capture_attempts_reflight_approved_by");
        builder.HasIndex(attempt => new
            {
                attempt.SupersededAttemptId,
                attempt.TenantId,
                attempt.FarmId,
                attempt.OrderId
            })
            .HasDatabaseName("ix_capture_attempts_parent_owner");
        builder.HasIndex(attempt => attempt.OrderId)
            .HasDatabaseName("uq_capture_attempts_active_order")
            .HasFilter("status IN ('PLANNED', 'PREFLIGHT_READY', 'IN_FLIGHT', 'AWAITING_UPLOAD', 'UPLOADED', 'PROCESSING')")
            .IsUnique();
        builder.HasIndex(attempt => attempt.SupersededAttemptId)
            .HasDatabaseName("uq_capture_attempts_parent_successor")
            .HasFilter("superseded_attempt_id IS NOT NULL")
            .IsUnique();
        builder.HasIndex(attempt => attempt.MappingSourceAttemptId)
            .HasDatabaseName("uq_capture_attempts_disease_root")
            .HasFilter("mapping_source_attempt_id IS NOT NULL AND superseded_attempt_id IS NULL")
            .IsUnique();
        builder.HasIndex(attempt => new { attempt.MappingSourceAttemptId, attempt.TenantId, attempt.FarmId, attempt.OrderId })
            .HasDatabaseName("ix_capture_attempts_mapping_source_owner");
        builder.HasOne<CaptureAttempt>()
            .WithMany()
            .HasForeignKey(attempt => new { attempt.MappingSourceAttemptId, attempt.TenantId, attempt.FarmId, attempt.OrderId })
            .HasPrincipalKey(source => new { source.Id, source.TenantId, source.FarmId, source.OrderId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_attempts_mapping_source_owner");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(attempt => new { attempt.OrderId, attempt.TenantId, attempt.FarmId })
            .HasPrincipalKey(order => new { order.Id, order.TenantId, order.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_attempts_order_same_owner");

        builder.HasOne<CaptureAttempt>()
            .WithMany()
            .HasForeignKey(attempt => new
            {
                attempt.SupersededAttemptId,
                attempt.TenantId,
                attempt.FarmId,
                attempt.OrderId
            })
            .HasPrincipalKey(parent => new
            {
                parent.Id,
                parent.TenantId,
                parent.FarmId,
                parent.OrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_attempts_parent_same_owner");

        builder.HasMany(attempt => attempt.History)
            .WithOne()
            .HasForeignKey(history => history.CaptureAttemptId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_attempt_history_attempt");
        builder.Navigation(attempt => attempt.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
