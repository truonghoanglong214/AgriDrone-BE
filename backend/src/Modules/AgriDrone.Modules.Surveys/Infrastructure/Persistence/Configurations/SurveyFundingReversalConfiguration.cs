using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyFundingReversalConfiguration
    : IEntityTypeConfiguration<SurveyFundingReversal>
{
    public void Configure(EntityTypeBuilder<SurveyFundingReversal> builder)
    {
        builder.ToTable(
            "survey_funding_reversals",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_funding_reversals_amount",
                    "amount > 0 AND amount = FLOOR(amount)");
                table.HasCheckConstraint("ck_survey_funding_reversals_currency", "currency = 'VND'");
                table.HasCheckConstraint("ck_survey_funding_reversals_time", "created_at >= occurred_at");
            });

        builder.HasKey(value => value.Id).HasName("pk_survey_funding_reversals");
        builder.Property(value => value.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(value => value.SurveyPaymentId).HasColumnName("survey_payment_id").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.DepositObligationId).HasColumnName("deposit_obligation_id").HasColumnType("uuid").IsRequired();
        builder.Property(value => value.Provider).HasColumnName("provider").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(value => value.ProviderEventId).HasColumnName("provider_event_id").HasColumnType("character varying(150)").HasMaxLength(150).IsRequired();
        builder.Property(value => value.EventType).HasColumnName("event_type").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(value => value.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(value => value.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(value => value.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(value => value.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(value => new { value.Provider, value.ProviderEventId })
            .HasDatabaseName("uq_survey_funding_reversals_provider_event")
            .IsUnique();
        builder.HasIndex(value => new { value.SurveyOrderId, value.OccurredAt })
            .HasDatabaseName("ix_survey_funding_reversals_order_occurred");

        builder.HasOne<SurveyPayment>()
            .WithMany()
            .HasForeignKey(value => value.SurveyPaymentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_reversals_payment");
        builder.HasOne<SurveyDepositObligation>()
            .WithMany()
            .HasForeignKey(value => new { value.DepositObligationId, value.SurveyOrderId })
            .HasPrincipalKey(value => new { value.Id, value.SurveyOrderId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_funding_reversals_obligation_order");
    }
}
