using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CaptureInputManifestConfiguration : IEntityTypeConfiguration<CaptureInputManifest>
{
    public void Configure(EntityTypeBuilder<CaptureInputManifest> builder)
    {
        builder.ToTable(
            "capture_input_manifests",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_capture_input_manifests_file_count", "total_file_count > 0");
                table.HasCheckConstraint("ck_capture_input_manifests_size", "total_size_bytes > 0");
                table.HasCheckConstraint("ck_capture_input_manifests_evaluated", "evaluated_at IS NULL OR evaluated_at >= created_at");
            });

        builder.HasKey(manifest => manifest.Id).HasName("pk_capture_input_manifests");
        builder.HasAlternateKey(manifest => new
        {
            manifest.Id,
            manifest.TenantId,
            manifest.FarmId,
            manifest.SurveyOrderId,
            manifest.CaptureAttemptId,
            manifest.MissionId,
            manifest.ManifestFingerprint
        }).HasName("uq_capture_input_manifests_identity");

        builder.Property(manifest => manifest.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(manifest => manifest.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(manifest => manifest.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(manifest => manifest.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(manifest => manifest.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid").IsRequired();
        builder.Property(manifest => manifest.MissionId).HasColumnName("mission_id").HasColumnType("uuid").IsRequired();
        builder.Property(manifest => manifest.ManifestFingerprint).HasColumnName("manifest_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(manifest => manifest.TotalFileCount).HasColumnName("total_file_count").HasColumnType("integer").IsRequired();
        builder.Property(manifest => manifest.TotalSizeBytes).HasColumnName("total_size_bytes").HasColumnType("bigint").IsRequired();
        builder.Property(manifest => manifest.GpsMetadataSummary).HasColumnName("gps_metadata_summary").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(manifest => manifest.Status).HasColumnName("status").HasColumnType("system.capture_input_manifest_status").IsRequired();
        builder.Property(manifest => manifest.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(manifest => manifest.EvaluatedAt).HasColumnName("evaluated_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(manifest => new { manifest.CaptureAttemptId, manifest.ManifestFingerprint })
            .HasDatabaseName("uq_capture_input_manifests_attempt_fingerprint")
            .IsUnique();
        builder.HasIndex(manifest => manifest.MissionId)
            .HasDatabaseName("ix_capture_input_manifests_mission");
        builder.HasIndex(manifest => new
            {
                manifest.CaptureAttemptId,
                manifest.TenantId,
                manifest.FarmId,
                manifest.SurveyOrderId
            })
            .HasDatabaseName("ix_capture_input_manifests_attempt_owner");

        builder.HasOne<CaptureAttempt>()
            .WithMany()
            .HasForeignKey(manifest => new
            {
                manifest.CaptureAttemptId,
                manifest.TenantId,
                manifest.FarmId,
                manifest.SurveyOrderId
            })
            .HasPrincipalKey(attempt => new
            {
                attempt.Id,
                attempt.TenantId,
                attempt.FarmId,
                attempt.OrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_capture_input_manifests_attempt_same_owner");
    }
}
