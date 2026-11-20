using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyRefundObligationConfiguration
    : IEntityTypeConfiguration<SurveyRefundObligation>
{
    public void Configure(EntityTypeBuilder<SurveyRefundObligation> builder)
    {
        builder.ToTable(
            "survey_refund_obligations",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_refund_obligations_amount",
                    "amount > 0 AND amount = FLOOR(amount)");
                table.HasCheckConstraint("ck_survey_refund_obligations_currency", "currency = 'VND'");
                table.HasCheckConstraint("ck_survey_refund_obligations_updated_at", "updated_at >= created_at");
                table.HasCheckConstraint(
                    "ck_survey_refund_obligations_status_time",
                    "(status = 'PENDING'::system.survey_refund_obligation_status) OR " +
                    "(status = 'RESERVED'::system.survey_refund_obligation_status AND reserved_at IS NOT NULL) OR " +
                    "(status = 'EXECUTING'::system.survey_refund_obligation_status AND reserved_at IS NOT NULL AND executing_at IS NOT NULL) OR " +
                    "(status = 'COMPLETED'::system.survey_refund_obligation_status AND reserved_at IS NOT NULL AND executing_at IS NOT NULL AND completed_at IS NOT NULL) OR " +
                    "(status = 'CANCELLED'::system.survey_refund_obligation_status AND cancelled_at IS NOT NULL)");
            });

        builder.HasKey(obligation => obligation.Id).HasName("pk_survey_refund_obligations");
        builder.Property(obligation => obligation.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(obligation => obligation.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.OriginKind).HasColumnName("origin_kind").HasColumnType("system.survey_refund_origin_kind").IsRequired();
        builder.Property(obligation => obligation.OriginDecisionId).HasColumnName("origin_decision_id").HasColumnType("uuid").IsRequired();
        builder.Property(obligation => obligation.FundingReceiptId).HasColumnName("funding_receipt_id").HasColumnType("uuid");
        builder.Property(obligation => obligation.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(obligation => obligation.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(obligation => obligation.Status).HasColumnName("status").HasColumnType("system.survey_refund_obligation_status").IsRequired();
        builder.Property(obligation => obligation.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(obligation => obligation.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(obligation => obligation.ReservedAt).HasColumnName("reserved_at").HasColumnType("timestamp with time zone");
        builder.Property(obligation => obligation.ExecutingAt).HasColumnName("executing_at").HasColumnType("timestamp with time zone");
        builder.Property(obligation => obligation.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.Property(obligation => obligation.CancelledAt).HasColumnName("cancelled_at").HasColumnType("timestamp with time zone");

        builder.HasIndex(obligation => new
            {
                obligation.SurveyOrderId,
                obligation.OriginKind,
                obligation.OriginDecisionId
            })
            .HasDatabaseName("uq_survey_refund_obligations_origin")
            .HasFilter("funding_receipt_id IS NULL")
            .IsUnique();
        builder.HasIndex(obligation => obligation.FundingReceiptId)
            .HasDatabaseName("uq_survey_refund_obligations_late_receipt")
            .HasFilter("funding_receipt_id IS NOT NULL")
            .IsUnique();
        builder.HasIndex(obligation => new { obligation.SurveyOrderId, obligation.Status })
            .HasDatabaseName("ix_survey_refund_obligations_order_status");

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(obligation => obligation.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_refund_obligations_order");

        builder.HasOne<SurveyCancellationDecision>()
            .WithMany()
            .HasForeignKey(obligation => new
            {
                obligation.OriginDecisionId,
                obligation.SurveyOrderId
            })
            .HasPrincipalKey(decision => new
            {
                decision.Id,
                decision.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_refund_obligations_decision_order");

        builder.HasOne<SurveyFundingReceipt>()
            .WithMany()
            .HasForeignKey(obligation => new
            {
                obligation.FundingReceiptId,
                obligation.SurveyOrderId
            })
            .HasPrincipalKey(receipt => new
            {
                receipt.Id,
                receipt.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_refund_obligations_receipt_order");
    }
}
