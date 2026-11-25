using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyPaymentConfiguration : IEntityTypeConfiguration<SurveyPayment>
{
    public void Configure(EntityTypeBuilder<SurveyPayment> builder)
    {
        builder.ToTable(
            "survey_payments",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_payments_amount_positive", "amount > 0");
                table.HasCheckConstraint("ck_survey_payments_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_survey_payments_confirmation", "(status = 'PAID'::system.survey_payment_status AND confirmed_at IS NOT NULL AND provider_reference IS NOT NULL) OR (status <> 'PAID'::system.survey_payment_status)");
                table.HasCheckConstraint("ck_survey_payments_provider_session", "status IN ('PENDING'::system.survey_payment_status, 'INVALIDATED'::system.survey_payment_status) OR provider_reference IS NOT NULL");
                table.HasCheckConstraint("ck_survey_payments_purpose", "purpose IN ('BaselineDeposit', 'FinalBalance', 'OrderDeposit')");
                table.HasCheckConstraint(
                    "ck_survey_payments_deposit_obligation",
                    "(purpose = 'OrderDeposit' AND deposit_obligation_id IS NOT NULL) OR " +
                    "(purpose <> 'OrderDeposit' AND deposit_obligation_id IS NULL)");
                table.HasCheckConstraint(
                    "ck_survey_payments_invoice_purpose",
                    "invoice_revision_id IS NULL OR purpose = 'FinalBalance'");
                table.HasCheckConstraint(
                    "ck_survey_payments_invalidation_audit",
                    "(status <> 'INVALIDATED'::system.survey_payment_status AND invalidated_at IS NULL AND invalidated_by IS NULL AND invalidation_reason IS NULL AND superseded_by_invoice_revision_id IS NULL) OR " +
                    "(status = 'INVALIDATED'::system.survey_payment_status AND invalidated_at IS NOT NULL AND invalidated_by IS NOT NULL AND invalidation_reason IS NOT NULL)");
            });
        builder.HasKey(payment => payment.Id).HasName("pk_survey_payments");
        builder.HasAlternateKey(payment => new { payment.Id, payment.SurveyOrderId }).HasName("uq_survey_payments_id_order");
        builder.Property(payment => payment.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()").ValueGeneratedOnAdd();
        builder.Property(payment => payment.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(payment => payment.DepositObligationId).HasColumnName("deposit_obligation_id").HasColumnType("uuid");
        builder.Property(payment => payment.InvoiceRevisionId).HasColumnName("invoice_revision_id").HasColumnType("uuid");
        builder.Property(payment => payment.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(payment => payment.Provider).HasColumnName("provider").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(payment => payment.ProviderReference).HasColumnName("provider_reference").HasColumnType("character varying(150)").HasMaxLength(150);
        builder.Property(payment => payment.Status).HasColumnName("status").HasColumnType("system.survey_payment_status").IsRequired();
        builder.Property(payment => payment.Purpose)
            .HasColumnName("purpose")
            .HasConversion<string>()
            .HasColumnType("character varying(50)")
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(payment => payment.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamp with time zone");
        builder.Property(payment => payment.InvalidatedAt).HasColumnName("invalidated_at").HasColumnType("timestamp with time zone");
        builder.Property(payment => payment.InvalidatedBy).HasColumnName("invalidated_by").HasColumnType("uuid");
        builder.Property(payment => payment.InvalidationReason).HasColumnName("invalidation_reason").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(payment => payment.SupersededByInvoiceRevisionId).HasColumnName("superseded_by_invoice_revision_id").HasColumnType("uuid");
        builder.Property(payment => payment.Version).IsRowVersion();
        builder.Property(payment => payment.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.Property(payment => payment.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("NOW()").IsRequired();
        builder.HasIndex(payment => new { payment.Provider, payment.ProviderReference }).HasDatabaseName("uq_survey_payments_provider_reference").HasFilter("provider_reference IS NOT NULL").IsUnique();
        builder.HasIndex(payment => new { payment.SurveyOrderId, payment.Purpose }).HasDatabaseName("uq_survey_payments_one_active_per_order_purpose").HasFilter("status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'PAID'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)").IsUnique();
        builder.HasIndex(payment => payment.DepositObligationId)
            .HasDatabaseName("uq_survey_payments_active_deposit_obligation")
            .HasFilter("deposit_obligation_id IS NOT NULL AND status IN ('PENDING'::system.survey_payment_status, 'PROCESSING'::system.survey_payment_status, 'PAID'::system.survey_payment_status, 'ADJUSTMENT_REQUIRED'::system.survey_payment_status)")
            .IsUnique();
        builder.HasIndex(payment => new { payment.Status, payment.UpdatedAt }).HasDatabaseName("ix_survey_payments_reconciliation");
        builder.HasIndex(payment => new { payment.InvoiceRevisionId, payment.SurveyOrderId }).HasDatabaseName("ix_survey_payments_invoice_revision");
        builder.HasIndex(payment => new { payment.SupersededByInvoiceRevisionId, payment.SurveyOrderId }).HasDatabaseName("ix_survey_payments_superseded_by_invoice_revision");
        builder.HasIndex(payment => payment.InvalidatedBy).HasDatabaseName("ix_survey_payments_invalidated_by");
        builder.HasOne(payment => payment.SurveyOrder).WithMany(order => order.Payments).HasForeignKey(payment => payment.SurveyOrderId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_survey_payments_orders_order_id");
        builder.HasOne<SurveyDepositObligation>()
            .WithMany()
            .HasForeignKey(payment => new { payment.DepositObligationId, payment.SurveyOrderId })
            .HasPrincipalKey(obligation => new { obligation.Id, obligation.SurveyOrderId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_payments_deposit_obligation_order");
        builder.HasOne<SurveyInvoiceRevision>()
            .WithMany()
            .HasForeignKey(payment => new { payment.InvoiceRevisionId, payment.SurveyOrderId })
            .HasPrincipalKey(invoice => new { invoice.Id, invoice.SurveyOrderId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_payments_invoice_revision");
        builder.HasOne<SurveyInvoiceRevision>()
            .WithMany()
            .HasForeignKey(payment => new { payment.SupersededByInvoiceRevisionId, payment.SurveyOrderId })
            .HasPrincipalKey(invoice => new { invoice.Id, invoice.SurveyOrderId })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_payments_superseded_by_invoice_revision");
    }
}

