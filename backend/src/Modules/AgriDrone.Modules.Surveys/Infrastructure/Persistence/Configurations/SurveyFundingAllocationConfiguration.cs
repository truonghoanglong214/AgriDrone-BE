using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyFundingAllocationConfiguration
    : IEntityTypeConfiguration<SurveyFundingAllocation>
{
    public void Configure(EntityTypeBuilder<SurveyFundingAllocation> builder)
    {
        builder.ToTable(
            "survey_funding_allocations",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_funding_allocations_amount",
                    "allocated_amount > 0 AND allocated_amount = FLOOR(allocated_amount)");
                table.HasCheckConstraint("ck_survey_funding_allocations_currency", "currency = 'VND'");
            });

        builder.HasKey(allocation => allocation.Id).HasName("pk_survey_funding_allocations");
        builder.Property(allocation => allocation.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(allocation => allocation.FundingReceiptId).HasColumnName("funding_receipt_id").HasColumnType("uuid").IsRequired();
        builder.Property(allocation => allocation.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(allocation => allocation.DepositObligationId).HasColumnName("deposit_obligation_id").HasColumnType("uuid").IsRequired();
        builder.Property(allocation => allocation.AllocatedAmount).HasColumnName("allocated_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(allocation => allocation.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(allocation => allocation.AllocatedAt).HasColumnName("allocated_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(allocation => new
            {
                allocation.FundingReceiptId,
                allocation.DepositObligationId
            })
            .HasDatabaseName("uq_survey_funding_allocations_receipt_obligation")
            .IsUnique();
        builder.HasIndex(allocation => new
            {
                allocation.SurveyOrderId,
                allocation.DepositObligationId,
                allocation.AllocatedAt
            })
            .HasDatabaseName("ix_survey_funding_allocations_order_obligation");

        builder.HasOne<SurveyFundingReceipt>()
            .WithMany()
            .HasForeignKey(allocation => new
            {
                allocation.FundingReceiptId,
                allocation.SurveyOrderId
            })
            .HasPrincipalKey(receipt => new
            {
                receipt.Id,
                receipt.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_allocations_receipt_order");

        builder.HasOne<SurveyDepositObligation>()
            .WithMany()
            .HasForeignKey(allocation => new
            {
                allocation.DepositObligationId,
                allocation.SurveyOrderId
            })
            .HasPrincipalKey(obligation => new
            {
                obligation.Id,
                obligation.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_allocations_obligation_order");
    }
}
