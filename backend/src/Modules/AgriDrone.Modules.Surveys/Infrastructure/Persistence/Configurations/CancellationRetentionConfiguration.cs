using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CancellationRetentionConfiguration
    : IEntityTypeConfiguration<CancellationRetention>
{
    public void Configure(EntityTypeBuilder<CancellationRetention> builder)
    {
        builder.ToTable(
            "cancellation_retentions",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_cancellation_retentions_amount",
                    "amount > 0 AND amount = FLOOR(amount)");
                table.HasCheckConstraint("ck_cancellation_retentions_currency", "currency = 'VND'");
            });

        builder.HasKey(retention => retention.Id).HasName("pk_cancellation_retentions");
        builder.Property(retention => retention.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(retention => retention.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(retention => retention.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(retention => retention.CancellationDecisionId).HasColumnName("cancellation_decision_id").HasColumnType("uuid").IsRequired();
        builder.Property(retention => retention.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(retention => retention.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(retention => retention.Reason).HasColumnName("reason").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(retention => retention.RecordedAt).HasColumnName("recorded_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(retention => retention.CancellationDecisionId)
            .HasDatabaseName("uq_cancellation_retentions_decision")
            .IsUnique();
        builder.HasIndex(retention => retention.SurveyOrderId)
            .HasDatabaseName("ix_cancellation_retentions_order");

        builder.HasOne<SurveyCancellationDecision>()
            .WithMany()
            .HasForeignKey(retention => new
            {
                retention.CancellationDecisionId,
                retention.SurveyOrderId
            })
            .HasPrincipalKey(decision => new
            {
                decision.Id,
                decision.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cancellation_retentions_decision_order");
    }
}
