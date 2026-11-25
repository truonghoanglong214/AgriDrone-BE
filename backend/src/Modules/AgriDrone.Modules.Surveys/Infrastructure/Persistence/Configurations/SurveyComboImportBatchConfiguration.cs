using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyComboImportBatchConfiguration
    : IEntityTypeConfiguration<SurveyComboImportBatch>
{
    public void Configure(EntityTypeBuilder<SurveyComboImportBatch> builder)
    {
        builder.ToTable(
            "survey_combo_import_batches",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_combo_import_batches_source_revision",
                    "source_revision >= 1");
                table.HasCheckConstraint(
                    "ck_survey_combo_import_batches_blocking_count",
                    "blocking_count >= 0");
                table.HasCheckConstraint(
                    "ck_survey_combo_import_batches_fingerprint",
                    "canonical_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_combo_import_batches_status",
                    "status IN ('PENDING_REVIEW', 'REVIEWED', 'SUPERSEDED')");
            });

        builder.HasKey(b => b.Id).HasName("pk_survey_combo_import_batches");
        builder.HasAlternateKey(b => new { b.Id, b.SurveyOrderId, b.TenantId, b.FarmId })
            .HasName("uq_survey_combo_import_batches_id_order_tenant_farm");

        builder.Property(b => b.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.MissionId).HasColumnName("mission_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.JobId).HasColumnName("job_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.SourceRevision).HasColumnName("source_revision").HasColumnType("integer").IsRequired();
        builder.Property(b => b.HandoffId).HasColumnName("handoff_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.PredecessorBatchId).HasColumnName("predecessor_batch_id").HasColumnType("uuid");
        builder.Property(b => b.SupersedesHandoffId).HasColumnName("supersedes_handoff_id").HasColumnType("uuid");
        builder.Property(b => b.BusinessPolicyVersionId).HasColumnName("business_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.InputApprovalId).HasColumnName("input_approval_id").HasColumnType("uuid");
        builder.Property(b => b.InputManifestId).HasColumnName("input_manifest_id").HasColumnType("uuid");
        builder.Property(b => b.InputManifestHash).HasColumnName("input_manifest_hash").HasColumnType("character varying(64)").HasMaxLength(64);
        builder.Property(b => b.MappingKind).HasColumnName("mapping_kind").HasColumnType("character varying(32)").HasMaxLength(32).IsRequired();
        builder.Property(b => b.MappingCandidateBatchId).HasColumnName("mapping_candidate_batch_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.FarmBaseMapVersionId).HasColumnName("farm_base_map_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.BaselineMapVersionId).HasColumnName("baseline_map_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(b => b.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(b => b.CanonicalFingerprint).HasColumnName("canonical_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(b => b.ProducedAt).HasColumnName("produced_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(b => b.ImportedAt).HasColumnName("imported_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(b => b.BlockingCount).HasColumnName("blocking_count").HasColumnType("integer").IsRequired();
        builder.Property(b => b.BlockingItems).HasColumnName("blocking_items").HasColumnType("jsonb").IsRequired();
        builder.Property(b => b.Status).HasColumnName("status").HasColumnType("character varying(32)").HasMaxLength(32).HasDefaultValue(SurveyComboImportBatch.StatusPendingReview).IsRequired();

        // Unique indexes
        builder.HasIndex(b => new { b.TenantId, b.HandoffId })
            .HasDatabaseName("uq_survey_combo_import_batches_tenant_handoff")
            .IsUnique();

        builder.HasIndex(b => new { b.TenantId, b.SurveyOrderId, b.CaptureAttemptId, b.JobId, b.SourceRevision })
            .HasDatabaseName("uq_survey_combo_import_batches_business_identity")
            .IsUnique();

        builder.HasIndex(b => b.CanonicalFingerprint)
            .HasDatabaseName("ix_survey_combo_import_batches_fingerprint");

        builder.HasIndex(b => new { b.SurveyOrderId, b.ImportedAt })
            .HasDatabaseName("ix_survey_combo_import_batches_order_imported");

        // Relationships within Survey module
        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(b => new { b.SurveyOrderId, b.TenantId, b.FarmId })
            .HasPrincipalKey(o => new { o.Id, o.TenantId, o.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_combo_import_batches_order");

        builder.HasOne<SurveyComboImportBatch>()
            .WithMany()
            .HasForeignKey(b => new { b.PredecessorBatchId, b.SurveyOrderId, b.TenantId, b.FarmId })
            .HasPrincipalKey(p => new { p.Id, p.SurveyOrderId, p.TenantId, p.FarmId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_combo_import_batches_predecessor");
    }
}
