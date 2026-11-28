using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyInvoiceRevisionConfiguration
    : IEntityTypeConfiguration<SurveyInvoiceRevision>
{
    public void Configure(EntityTypeBuilder<SurveyInvoiceRevision> builder)
    {
        builder.ToTable(
            "survey_invoice_revisions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_revision",
                    "revision_number >= 1");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_lineage",
                    "(revision_number = 1 AND predecessor_revision_id IS NULL) OR " +
                    "(revision_number > 1 AND predecessor_revision_id IS NOT NULL AND predecessor_revision_id <> id)");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_pole_count",
                    "verified_billable_pole_count >= 0");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_price",
                    "price_per_pole > 0 AND price_per_pole = FLOOR(price_per_pole)");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_currency",
                    "currency = 'VND'");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_amounts",
                    "total_amount >= 0 AND total_amount = FLOOR(total_amount) AND " +
                    "total_amount = CASE WHEN plant_health_pole_count IS NULL THEN verified_billable_pole_count * price_per_pole ELSE verified_billable_pole_count * mapping_price_per_pole + plant_health_pole_count * plant_health_price_per_pole END AND " +
                    "valid_allocated_deposit_net >= 0 AND valid_allocated_deposit_net = FLOOR(valid_allocated_deposit_net) AND " +
                    "due_amount >= 0 AND due_amount = FLOOR(due_amount) AND " +
                    "due_amount = GREATEST(0, total_amount - valid_allocated_deposit_net) AND " +
                    "overpayment_amount >= 0 AND overpayment_amount = FLOOR(overpayment_amount) AND " +
                    "overpayment_amount = GREATEST(0, valid_allocated_deposit_net - total_amount)");
                table.HasCheckConstraint("ck_survey_invoice_revisions_components",
                    "(plant_health_pole_count IS NULL AND mapping_price_per_pole IS NULL AND plant_health_price_per_pole IS NULL) OR (plant_health_pole_count IS NOT NULL AND mapping_price_per_pole IS NOT NULL AND plant_health_price_per_pole IS NOT NULL AND plant_health_pole_count BETWEEN 0 AND verified_billable_pole_count AND mapping_price_per_pole > 0 AND mapping_price_per_pole = FLOOR(mapping_price_per_pole) AND plant_health_price_per_pole > 0 AND plant_health_price_per_pole = FLOOR(plant_health_price_per_pole) AND price_per_pole = mapping_price_per_pole + plant_health_price_per_pole)");
                table.HasCheckConstraint("ck_survey_invoice_revisions_health_area", "plant_health_survey_area IS NULL OR (plant_health_pole_count IS NOT NULL AND NOT ST_IsEmpty(plant_health_survey_area) AND ST_IsValid(plant_health_survey_area))");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_zero_pole",
                    "(verified_billable_pole_count > 0 AND zero_pole_approved_by IS NULL AND zero_pole_reason_code IS NULL AND zero_pole_evidence_reference IS NULL) OR " +
                    "(verified_billable_pole_count = 0 AND zero_pole_approved_by IS NOT NULL AND zero_pole_reason_code IS NOT NULL AND zero_pole_evidence_reference IS NOT NULL)");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_supersede_audit",
                    "(status = 'CURRENT' AND superseded_by IS NULL AND superseded_at IS NULL AND superseded_reason IS NULL AND successor_revision_id IS NULL) OR " +
                    "(status = 'SUPERSEDED' AND superseded_by IS NOT NULL AND superseded_at IS NOT NULL AND superseded_reason IS NOT NULL AND successor_revision_id IS NOT NULL AND successor_revision_id <> id)");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_fingerprint",
                    "evidence_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_invoice_revisions_status",
                    "status IN ('CURRENT', 'SUPERSEDED')");
            });

        builder.HasKey(i => i.Id).HasName("pk_survey_invoice_revisions");
        builder.HasAlternateKey(i => new { i.Id, i.SurveyOrderId, i.TenantId, i.FarmId })
            .HasName("uq_survey_invoice_revisions_composite_scope");
        builder.HasAlternateKey(i => new { i.Id, i.SurveyOrderId })
            .HasName("uq_survey_invoice_revisions_id_order");
        builder.HasAlternateKey(i => new { i.SurveyOrderId, i.RevisionNumber })
            .HasName("uq_survey_invoice_revisions_order_revision");

        builder.Property(i => i.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.FarmId).HasColumnName("farm_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.RevisionNumber).HasColumnName("revision_number").HasColumnType("integer").IsRequired();
        builder.Property(i => i.PredecessorRevisionId).HasColumnName("predecessor_revision_id").HasColumnType("uuid");
        builder.Property(i => i.ApprovedResultRevisionId).HasColumnName("approved_result_revision_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.PoleCountSnapshotId).HasColumnName("pole_count_snapshot_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.VerifiedBillablePoleCount).HasColumnName("verified_billable_pole_count").HasColumnType("integer").IsRequired();
        builder.Property(i => i.SurveyServicePriceId).HasColumnName("survey_service_price_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.PricePerPole).HasColumnName("price_per_pole").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.PlantHealthPoleCount).HasColumnName("plant_health_pole_count");
        builder.Property(i => i.MappingPricePerPole).HasColumnName("mapping_price_per_pole").HasPrecision(18, 2);
        builder.Property(i => i.PlantHealthPricePerPole).HasColumnName("plant_health_price_per_pole").HasPrecision(18, 2);
        builder.Property(i => i.PlantHealthSurveyArea).HasColumnName("plant_health_survey_area").HasColumnType("geometry(Polygon,4326)");
        builder.Property(i => i.Currency).HasColumnName("currency").HasColumnType("character varying(3)").HasMaxLength(3).HasDefaultValue("VND").IsRequired();
        builder.Property(i => i.CommercialPolicyVersionId).HasColumnName("commercial_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.ValidAllocatedDepositNet).HasColumnName("valid_allocated_deposit_net").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.DueAmount).HasColumnName("due_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.OverpaymentAmount).HasColumnName("overpayment_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(i => i.EvidenceFingerprint).HasColumnName("evidence_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(i => i.FinalizedBy).HasColumnName("finalized_by").HasColumnType("uuid").IsRequired();
        builder.Property(i => i.FinalizedAt).HasColumnName("finalized_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(i => i.FinalizationReason).HasColumnName("finalization_reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(i => i.Status).HasColumnName("status").HasColumnType("character varying(32)").HasMaxLength(32).HasDefaultValue(SurveyInvoiceRevision.StatusCurrent).IsRequired();
        builder.Property(i => i.ZeroPoleApprovedBy).HasColumnName("zero_pole_approved_by").HasColumnType("uuid");
        builder.Property(i => i.ZeroPoleReasonCode).HasColumnName("zero_pole_reason_code").HasColumnType("character varying(64)").HasMaxLength(64);
        builder.Property(i => i.ZeroPoleEvidenceReference).HasColumnName("zero_pole_evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(i => i.SupersededBy).HasColumnName("superseded_by").HasColumnType("uuid");
        builder.Property(i => i.SupersededAt).HasColumnName("superseded_at").HasColumnType("timestamp with time zone");
        builder.Property(i => i.SupersededReason).HasColumnName("superseded_reason").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(i => i.SuccessorRevisionId).HasColumnName("successor_revision_id").HasColumnType("uuid");

        builder.HasIndex(i => i.SurveyOrderId)
            .HasDatabaseName("uq_survey_invoice_revisions_one_current")
            .IsUnique()
            .HasFilter("status = 'CURRENT'");

        builder.HasOne<SurveyOrder>()
            .WithMany(o => o.InvoiceRevisions)
            .HasForeignKey(i => new { i.SurveyOrderId, i.TenantId, i.FarmId })
            .HasPrincipalKey(o => new { o.Id, o.TenantId, o.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_invoice_revisions_order");

        builder.HasOne<SurveyInternalApprovedResultRevision>()
            .WithMany()
            .HasForeignKey(i => new { i.ApprovedResultRevisionId, i.SurveyOrderId, i.TenantId, i.FarmId })
            .HasPrincipalKey(r => new { r.Id, r.SurveyOrderId, r.TenantId, r.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_invoice_revisions_approved_result_scope");

        builder.HasOne<SurveyOrderPoleCountSnapshot>()
            .WithMany()
            .HasForeignKey(i => new { i.PoleCountSnapshotId, i.SurveyOrderId, i.TenantId, i.FarmId })
            .HasPrincipalKey(s => new { s.Id, s.SurveyOrderId, s.TenantId, s.FarmId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_invoice_revisions_snapshot_scope");

        builder.HasOne<SurveyInvoiceRevision>()
            .WithMany()
            .HasForeignKey(i => new { i.PredecessorRevisionId, i.SurveyOrderId })
            .HasPrincipalKey(p => new { p.Id, p.SurveyOrderId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_invoice_revisions_predecessor_scope");
    }
}
