using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyInternalApprovedResultRevisionConfiguration
    : IEntityTypeConfiguration<SurveyInternalApprovedResultRevision>
{
    public void Configure(EntityTypeBuilder<SurveyInternalApprovedResultRevision> builder)
    {
        builder.ToTable(
            "survey_internal_approved_result_revisions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_revision",
                    "revision_number >= 1");
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_lineage",
                    "(revision_number = 1 AND predecessor_revision_id IS NULL) OR " +
                    "(revision_number > 1 AND predecessor_revision_id IS NOT NULL AND predecessor_revision_id <> id)");
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_service_code",
                    "service_code IN ('MAP', 'COMBO')");
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_combo_rules",
                    "((service_code = 'MAP' AND NOT screens_disease AND combo_batch_id IS NULL AND combo_review_revision_id IS NULL AND combo_review_fingerprint IS NULL AND health_assessment_status = 'NotAssessed' AND limitation_notice IS NULL) OR " +
                    "(service_code = 'COMBO' AND screens_disease AND combo_batch_id IS NOT NULL AND combo_review_revision_id IS NOT NULL AND combo_review_fingerprint IS NOT NULL AND health_assessment_status IN ('SuspectedObservationsApproved', 'NoObservationsDetected') AND limitation_notice IS NOT NULL))");
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_combo_review_fingerprint",
                    "combo_review_fingerprint IS NULL OR combo_review_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_supersede_audit",
                    "(is_current = true AND superseded_by IS NULL AND superseded_at IS NULL AND superseded_reason IS NULL AND successor_revision_id IS NULL) OR " +
                    "(is_current = false AND superseded_by IS NOT NULL AND superseded_at IS NOT NULL AND superseded_reason IS NOT NULL AND successor_revision_id IS NOT NULL AND successor_revision_id <> id)");
                table.HasCheckConstraint(
                    "ck_survey_internal_approved_result_revisions_fingerprint",
                    "evidence_fingerprint ~ '^[0-9a-f]{64}$'");
            });

        builder.HasKey(r => r.Id).HasName("pk_survey_internal_approved_result_revisions");
        builder.HasAlternateKey(r => new { r.Id, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasName("uq_survey_internal_approved_result_revisions_composite_scope");
        builder.HasAlternateKey(r => new { r.Id, r.SurveyOrderId })
            .HasName("uq_survey_internal_approved_result_revisions_id_order");
        builder.HasAlternateKey(r => new { r.SurveyOrderId, r.RevisionNumber })
            .HasName("uq_survey_internal_approved_result_revisions_order_revision");

        builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.RevisionNumber).HasColumnName("revision_number").HasColumnType("integer").IsRequired();
        builder.Property(r => r.PredecessorRevisionId).HasColumnName("predecessor_revision_id").HasColumnType("uuid");
        builder.Property(r => r.ServiceCode).HasColumnName("service_code").HasColumnType("character varying(32)").HasMaxLength(32).IsRequired();
        builder.Property(r => r.ScreensDisease).HasColumnName("screens_disease").HasColumnType("boolean").IsRequired();
        builder.Property(r => r.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.MissionId).HasColumnName("mission_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.FarmBaseMapVersionId).HasColumnName("farm_base_map_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.BaselineMapVersionId).HasColumnName("baseline_map_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.FarmBoundaryVersionId).HasColumnName("farm_boundary_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.PoleCountSnapshotId).HasColumnName("pole_count_snapshot_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.MappingCandidateBatchId).HasColumnName("mapping_candidate_batch_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.ComboBatchId).HasColumnName("combo_batch_id").HasColumnType("uuid");
        builder.Property(r => r.ComboReviewRevisionId).HasColumnName("combo_review_revision_id").HasColumnType("uuid");
        builder.Property(r => r.ComboReviewFingerprint).HasColumnName("combo_review_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64);
        builder.Property(r => r.ReviewDecision).HasColumnName("review_decision").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.HealthAssessmentStatus).HasColumnName("health_assessment_status").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.LimitationNotice).HasColumnName("limitation_notice").HasColumnType("character varying(1000)").HasMaxLength(1000);
        builder.Property(r => r.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(r => r.ReviewReason).HasColumnName("review_reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(r => r.EvidenceFingerprint).HasColumnName("evidence_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.IsCurrent).HasColumnName("is_current").HasColumnType("boolean").HasDefaultValue(true).IsRequired();
        builder.Property(r => r.SupersededBy).HasColumnName("superseded_by").HasColumnType("uuid");
        builder.Property(r => r.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(r => r.SupersededReason).HasColumnName("superseded_reason").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(r => r.SuccessorRevisionId).HasColumnName("successor_revision_id").HasColumnType("uuid");

        builder.HasIndex(r => r.SurveyOrderId)
            .HasDatabaseName("uq_survey_internal_approved_result_revisions_one_current")
            .IsUnique()
            .HasFilter("is_current = true");

        builder.HasIndex(r => new { r.ComboBatchId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasDatabaseName("ix_survey_internal_approved_result_revisions_combo_batch");

        builder.HasIndex(r => new { r.ComboReviewRevisionId, r.ComboBatchId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasDatabaseName("ix_survey_internal_approved_result_revisions_combo_review");

        builder.HasOne<SurveyOrder>()
            .WithMany(o => o.ApprovedResultRevisions)
            .HasForeignKey(r => new { r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(o => new { o.Id, o.TenantId, o.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_internal_approved_result_revisions_order");

        builder.HasOne<SurveyOrderPoleCountSnapshot>()
            .WithMany()
            .HasForeignKey(r => new { r.PoleCountSnapshotId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(s => new { s.Id, s.SurveyOrderId, s.TenantId, s.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_internal_approved_result_revisions_snapshot_scope");

        builder.HasOne<SurveyInternalApprovedResultRevision>()
            .WithMany()
            .HasForeignKey(r => new { r.PredecessorRevisionId, r.SurveyOrderId })
            .HasPrincipalKey(p => new { p.Id, p.SurveyOrderId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_internal_approved_result_revisions_predecessor_scope");

        builder.HasOne<SurveyComboImportBatch>()
            .WithMany()
            .HasForeignKey(r => new { r.ComboBatchId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(b => new { b.Id, b.SurveyOrderId, b.TenantId, b.FarmId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_internal_approved_result_revisions_combo_batch");

        builder.HasOne<SurveyComboReviewRevision>()
            .WithMany()
            .HasForeignKey(r => new { r.ComboReviewRevisionId, r.ComboBatchId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(rv => new { rv.Id, rv.ComboBatchId, rv.SurveyOrderId, rv.TenantId, rv.FarmId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_internal_approved_result_revisions_combo_review");
    }
}
