using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CaptureInputApprovalConfiguration : IEntityTypeConfiguration<CaptureInputApproval>
{
    public void Configure(EntityTypeBuilder<CaptureInputApproval> builder)
    {
        builder.ToTable("capture_input_approvals", "survey");
        builder.HasKey(approval => approval.Id).HasName("pk_capture_input_approvals");
        builder.Property(approval => approval.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(approval => approval.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.MissionId).HasColumnName("mission_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.InputManifestId).HasColumnName("input_manifest_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.ManifestFingerprint).HasColumnName("manifest_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(approval => approval.ProcessingJobId).HasColumnName("processing_job_id").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.ApprovedBy).HasColumnName("approved_by").HasColumnType("uuid").IsRequired();
        builder.Property(approval => approval.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(approval => approval.TechnicalQualityNotes).HasColumnName("technical_quality_notes").HasColumnType("character varying(500)").HasMaxLength(500);

        builder.HasIndex(approval => approval.InputManifestId)
            .HasDatabaseName("uq_capture_input_approvals_manifest")
            .IsUnique();
        builder.HasIndex(approval => approval.ProcessingJobId)
            .HasDatabaseName("uq_capture_input_approvals_processing_job")
            .IsUnique();
        builder.HasIndex(approval => approval.ApprovedBy)
            .HasDatabaseName("ix_capture_input_approvals_approved_by");
        builder.HasIndex(approval => approval.MissionId)
            .HasDatabaseName("ix_capture_input_approvals_mission");
        builder.HasIndex(approval => new
            {
                approval.InputManifestId,
                approval.TenantId,
                approval.FarmId,
                approval.SurveyOrderId,
                approval.CaptureAttemptId,
                approval.MissionId,
                approval.ManifestFingerprint
            })
            .HasDatabaseName("ix_capture_input_approvals_manifest_identity");

        builder.HasOne<CaptureInputManifest>()
            .WithMany()
            .HasForeignKey(approval => new
            {
                approval.InputManifestId,
                approval.TenantId,
                approval.FarmId,
                approval.SurveyOrderId,
                approval.CaptureAttemptId,
                approval.MissionId,
                approval.ManifestFingerprint
            })
            .HasPrincipalKey(manifest => new
            {
                manifest.Id,
                manifest.TenantId,
                manifest.FarmId,
                manifest.SurveyOrderId,
                manifest.CaptureAttemptId,
                manifest.MissionId,
                manifest.ManifestFingerprint
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_input_approvals_manifest_identity");
    }
}
