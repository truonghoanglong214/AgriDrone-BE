using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyFundingReceiptConfiguration
    : IEntityTypeConfiguration<SurveyFundingReceipt>
{
    public void Configure(EntityTypeBuilder<SurveyFundingReceipt> builder)
    {
        builder.ToTable(
            "survey_funding_receipts",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_funding_receipts_amount",
                    "amount > 0 AND amount = FLOOR(amount)");
                table.HasCheckConstraint("ck_survey_funding_receipts_currency", "currency = 'VND'");
                table.HasCheckConstraint("ck_survey_funding_receipts_received_at", "created_at >= received_at");
            });

        builder.HasKey(receipt => receipt.Id).HasName("pk_survey_funding_receipts");
        builder.HasAlternateKey(receipt => new { receipt.Id, receipt.SurveyOrderId })
            .HasName("uq_survey_funding_receipts_id_order");

        builder.Property(receipt => receipt.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(receipt => receipt.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(receipt => receipt.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(receipt => receipt.DepositObligationId).HasColumnName("deposit_obligation_id").HasColumnType("uuid");
        builder.Property(receipt => receipt.Provider).HasColumnName("provider").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(receipt => receipt.ProviderTransactionId).HasColumnName("provider_transaction_id").HasColumnType("character varying(150)").HasMaxLength(150).IsRequired();
        builder.Property(receipt => receipt.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(receipt => receipt.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(receipt => receipt.PaymentReference).HasColumnName("payment_reference").HasColumnType("character varying(150)").HasMaxLength(150).IsRequired();
        builder.Property(receipt => receipt.PayloadFingerprint).HasColumnName("payload_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(receipt => receipt.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(receipt => new { receipt.Provider, receipt.ProviderTransactionId })
            .HasDatabaseName("uq_survey_funding_receipts_provider_transaction")
            .IsUnique();
        builder.HasIndex(receipt => new { receipt.SurveyOrderId, receipt.ReceivedAt })
            .HasDatabaseName("ix_survey_funding_receipts_order_received");
        builder.HasIndex(receipt => receipt.DepositObligationId)
            .HasDatabaseName("ix_survey_funding_receipts_deposit_obligation");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(receipt => receipt.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_receipts_order");

        builder.HasOne<SurveyDepositObligation>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.DepositObligationId,
                receipt.SurveyOrderId
            })
            .HasPrincipalKey(obligation => new
            {
                obligation.Id,
                obligation.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_receipts_deposit_order");
    }
}
