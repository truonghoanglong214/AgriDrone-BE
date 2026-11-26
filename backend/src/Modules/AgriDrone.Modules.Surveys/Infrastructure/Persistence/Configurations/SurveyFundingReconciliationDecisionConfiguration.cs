using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyFundingReconciliationDecisionConfiguration
    : IEntityTypeConfiguration<SurveyFundingReconciliationDecision>
{
    public void Configure(EntityTypeBuilder<SurveyFundingReconciliationDecision> builder)
    {
        builder.ToTable(
            "survey_funding_reconciliation_decisions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_disposition",
                    "disposition_kind IN ('KeepExcluded', 'AllocateToCurrentInvoice', 'RouteToRefund')");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_amount",
                    "amount >= 0 AND amount = FLOOR(amount) AND (disposition_kind = 'KeepExcluded' OR amount > 0)");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_currency",
                    "currency = 'VND'");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_target",
                    "survey_payment_id IS NOT NULL OR funding_receipt_id IS NOT NULL");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_invoice_target",
                    "disposition_kind <> 'AllocateToCurrentInvoice' OR target_invoice_revision_id IS NOT NULL");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_fingerprint",
                    "payload_fingerprint ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_order_version",
                    "expected_order_version >= 1");
                table.HasCheckConstraint(
                    "ck_survey_funding_reconciliation_decisions_time",
                    "created_at >= reconciled_at");
            });

        builder.HasKey(d => d.Id).HasName("pk_survey_funding_reconciliation_decisions");

        builder.Property(d => d.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(d => d.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(d => d.OperationId).HasColumnName("operation_id").HasColumnType("uuid").IsRequired();
        builder.Property(d => d.PayloadFingerprint).HasColumnName("payload_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(d => d.DispositionKind)
            .HasColumnName("disposition_kind")
            .HasConversion<string>()
            .HasColumnType("character varying(32)")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(d => d.SurveyPaymentId).HasColumnName("survey_payment_id").HasColumnType("uuid");
        builder.Property(d => d.FundingReceiptId).HasColumnName("funding_receipt_id").HasColumnType("uuid");
        builder.Property(d => d.TargetInvoiceRevisionId).HasColumnName("target_invoice_revision_id").HasColumnType("uuid");
        builder.Property(d => d.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(d => d.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(d => d.ProviderReference).HasColumnName("provider_reference").HasColumnType("character varying(150)").HasMaxLength(150);
        builder.Property(d => d.ProviderTransactionId).HasColumnName("provider_transaction_id").HasColumnType("character varying(150)").HasMaxLength(150);
        builder.Property(d => d.Reason).HasColumnName("reason").HasColumnType("character varying(2000)").HasMaxLength(2000).IsRequired();
        builder.Property(d => d.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(1000)").HasMaxLength(1000).IsRequired();
        builder.Property(d => d.ExpectedOrderVersion).HasColumnName("expected_order_version").HasConversion<long>().HasColumnType("bigint").IsRequired();
        builder.Property(d => d.ReconciledBy).HasColumnName("reconciled_by").HasColumnType("uuid").IsRequired();
        builder.Property(d => d.ReconciledAt).HasColumnName("reconciled_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(d => new { d.SurveyOrderId, d.OperationId })
            .HasDatabaseName("uq_survey_funding_reconciliation_decisions_order_operation")
            .IsUnique();
        builder.HasIndex(d => new { d.SurveyOrderId, d.ReconciledAt, d.Id })
            .HasDatabaseName("ix_survey_funding_reconciliation_decisions_order_time");
        builder.HasIndex(d => d.FundingReceiptId)
            .HasDatabaseName("uq_survey_reconciliation_final_receipt")
            .IsUnique()
            .HasFilter("funding_receipt_id IS NOT NULL AND disposition_kind IN ('AllocateToCurrentInvoice', 'RouteToRefund')");
        builder.HasIndex(d => new { d.FundingReceiptId, d.SurveyOrderId })
            .HasDatabaseName("ix_survey_funding_reconciliation_decisions_receipt");
        builder.HasIndex(d => new { d.SurveyPaymentId, d.SurveyOrderId })
            .HasDatabaseName("ix_survey_funding_reconciliation_decisions_payment");
        builder.HasIndex(d => new { d.TargetInvoiceRevisionId, d.SurveyOrderId })
            .HasDatabaseName("ix_survey_funding_reconciliation_decisions_invoice");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(d => d.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_reconciliation_decisions_order");

        builder.HasOne<SurveyPayment>()
            .WithMany()
            .HasForeignKey(d => new { d.SurveyPaymentId, d.SurveyOrderId })
            .HasPrincipalKey(p => new { p.Id, p.SurveyOrderId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_reconciliation_decisions_payment_order");

        builder.HasOne<SurveyFundingReceipt>()
            .WithMany()
            .HasForeignKey(d => new { d.FundingReceiptId, d.SurveyOrderId })
            .HasPrincipalKey(r => new { r.Id, r.SurveyOrderId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_reconciliation_decisions_receipt_order");

        builder.HasOne<SurveyInvoiceRevision>()
            .WithMany()
            .HasForeignKey(d => new { d.TargetInvoiceRevisionId, d.SurveyOrderId })
            .HasPrincipalKey(i => new { i.Id, i.SurveyOrderId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_reconciliation_decisions_invoice_order");
    }
}
