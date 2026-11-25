using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyComboReviewRevisionConfiguration
    : IEntityTypeConfiguration<SurveyComboReviewRevision>
{
    public void Configure(EntityTypeBuilder<SurveyComboReviewRevision> builder)
    {
        builder.ToTable(
            "survey_combo_review_revisions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_source_revision",
                    "source_revision >= 1");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_revision",
                    "revision_number >= 1");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_lineage",
                    "(revision_number = 1 AND predecessor_review_id IS NULL) OR " +
                    "(revision_number > 1 AND predecessor_review_id IS NOT NULL AND predecessor_review_id <> id)");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_batch_fingerprint",
                    "batch_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_decision_fingerprint",
                    "decision_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_outcome",
                    "outcome IN ('Approved', 'Rejected')");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_counts",
                    "findings_count >= 0 AND accepted_findings_count >= 0 AND excluded_findings_count >= 0 AND " +
                    "accepted_findings_count + excluded_findings_count = findings_count AND " +
                    "zones_count >= 0 AND approved_zones_count >= 0 AND rejected_zones_count >= 0 AND " +
                    "approved_zones_count + rejected_zones_count = zones_count AND " +
                    "blockers_count >= 0 AND resolved_blockers_count >= 0 AND unresolved_blockers_count >= 0 AND " +
                    "resolved_blockers_count + unresolved_blockers_count = blockers_count");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_approved_blockers",
                    "outcome <> 'Approved' OR unresolved_blockers_count = 0");
                table.HasCheckConstraint(
                    "ck_survey_combo_review_revisions_supersede_audit",
                    "(is_current = true AND superseded_by IS NULL AND superseded_at IS NULL AND superseded_reason IS NULL AND successor_review_id IS NULL) OR " +
                    "(is_current = false AND superseded_by IS NOT NULL AND superseded_at IS NOT NULL AND superseded_reason IS NOT NULL AND successor_review_id IS NOT NULL AND successor_review_id <> id)");
            });

        builder.HasKey(r => r.Id).HasName("pk_survey_combo_review_revisions");
        builder.HasAlternateKey(r => new { r.Id, r.ComboBatchId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasName("uq_survey_combo_review_revisions_id_batch_order_tenant_farm");
        builder.HasAlternateKey(r => new { r.Id, r.ComboBatchId })
            .HasName("uq_survey_combo_review_revisions_id_batch");
        builder.HasAlternateKey(r => new { r.ComboBatchId, r.RevisionNumber })
            .HasName("uq_survey_combo_review_revisions_batch_revision");

        builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.CaptureAttemptId).HasColumnName("capture_attempt_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.ComboBatchId).HasColumnName("combo_batch_id").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.SourceRevision).HasColumnName("source_revision").HasColumnType("integer").IsRequired();
        builder.Property(r => r.BatchFingerprint).HasColumnName("batch_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.RevisionNumber).HasColumnName("revision_number").HasColumnType("integer").IsRequired();
        builder.Property(r => r.PredecessorReviewId).HasColumnName("predecessor_review_id").HasColumnType("uuid");
        builder.Property(r => r.Outcome).HasColumnName("outcome").HasColumnType("character varying(32)").HasMaxLength(32).IsRequired();
        builder.Property(r => r.ReviewReason).HasColumnName("review_reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(r => r.ConfirmNoDiseaseObserved).HasColumnName("confirm_no_disease_observed").HasColumnType("boolean").HasDefaultValue(false).IsRequired();
        builder.Property(r => r.NegativeResultConfirmationEvidence).HasColumnName("negative_result_confirmation_evidence").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(r => r.ReviewedBy).HasColumnName("reviewed_by").HasColumnType("uuid").IsRequired();
        builder.Property(r => r.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(r => r.FindingsCount).HasColumnName("findings_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.AcceptedFindingsCount).HasColumnName("accepted_findings_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.ExcludedFindingsCount).HasColumnName("excluded_findings_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.ZonesCount).HasColumnName("zones_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.ApprovedZonesCount).HasColumnName("approved_zones_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.RejectedZonesCount).HasColumnName("rejected_zones_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.BlockersCount).HasColumnName("blockers_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.ResolvedBlockersCount).HasColumnName("resolved_blockers_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.UnresolvedBlockersCount).HasColumnName("unresolved_blockers_count").HasColumnType("integer").IsRequired();
        builder.Property(r => r.DecisionsPayload).HasColumnName("decisions_payload").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.DecisionFingerprint).HasColumnName("decision_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(r => r.IsCurrent).HasColumnName("is_current").HasColumnType("boolean").HasDefaultValue(true).IsRequired();
        builder.Property(r => r.SupersededBy).HasColumnName("superseded_by").HasColumnType("uuid");
        builder.Property(r => r.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(r => r.SupersededReason).HasColumnName("superseded_reason").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(r => r.SuccessorReviewId).HasColumnName("successor_review_id").HasColumnType("uuid");

        builder.HasIndex(r => r.ComboBatchId)
            .HasDatabaseName("uq_survey_combo_review_revisions_one_current")
            .IsUnique()
            .HasFilter("is_current = true");

        builder.HasIndex(r => new { r.ComboBatchId, r.PredecessorReviewId })
            .HasDatabaseName("uq_survey_combo_review_revisions_predecessor_unique")
            .IsUnique()
            .HasFilter("predecessor_review_id IS NOT NULL");

        builder.HasIndex(r => new { r.SurveyOrderId, r.CaptureAttemptId, r.ComboBatchId })
            .HasDatabaseName("ix_survey_combo_review_revisions_order_attempt");

        builder.HasIndex(r => r.DecisionFingerprint)
            .HasDatabaseName("ix_survey_combo_review_revisions_decision_fingerprint");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(r => new { r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(o => new { o.Id, o.TenantId, o.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_combo_review_revisions_order");

        builder.HasOne<SurveyComboImportBatch>()
            .WithMany()
            .HasForeignKey(r => new { r.ComboBatchId, r.SurveyOrderId, r.TenantId, r.FarmId })
            .HasPrincipalKey(b => new { b.Id, b.SurveyOrderId, b.TenantId, b.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_combo_review_revisions_batch");

        builder.HasOne<SurveyComboReviewRevision>()
            .WithMany()
            .HasForeignKey(r => new { r.PredecessorReviewId, r.ComboBatchId })
            .HasPrincipalKey(p => new { p.Id, p.ComboBatchId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_combo_review_revisions_predecessor");
    }
}
