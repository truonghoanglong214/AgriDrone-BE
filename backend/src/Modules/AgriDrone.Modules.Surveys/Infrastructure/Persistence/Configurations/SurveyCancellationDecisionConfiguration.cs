using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyCancellationDecisionConfiguration
    : IEntityTypeConfiguration<SurveyCancellationDecision>
{
    public void Configure(EntityTypeBuilder<SurveyCancellationDecision> builder)
    {
        builder.ToTable(
            "survey_cancellation_decisions",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_survey_cancellation_decisions_policy_version", "cancellation_policy_version >= 1");
                table.HasCheckConstraint("ck_survey_cancellation_decisions_free_reschedules", "allowed_free_no_show_reschedules >= 0");
                table.HasCheckConstraint(
                    "ck_survey_cancellation_decisions_amounts",
                    "confirmed_receipts_amount >= 0 AND completed_refunds_amount >= 0 AND " +
                    "active_refund_reservations_amount >= 0 AND pinned_deposit_amount >= 0 AND " +
                    "retained_amount >= 0 AND refund_amount >= 0 AND " +
                    "confirmed_receipts_amount = FLOOR(confirmed_receipts_amount) AND " +
                    "completed_refunds_amount = FLOOR(completed_refunds_amount) AND " +
                    "active_refund_reservations_amount = FLOOR(active_refund_reservations_amount) AND " +
                    "pinned_deposit_amount = FLOOR(pinned_deposit_amount) AND " +
                    "retained_amount = FLOOR(retained_amount) AND refund_amount = FLOOR(refund_amount)");
                table.HasCheckConstraint(
                    "ck_survey_cancellation_decisions_arithmetic",
                    "confirmed_receipts_amount >= completed_refunds_amount + active_refund_reservations_amount AND " +
                    "retained_amount <= LEAST(pinned_deposit_amount, confirmed_receipts_amount - completed_refunds_amount - active_refund_reservations_amount) AND " +
                    "refund_amount = confirmed_receipts_amount - completed_refunds_amount - active_refund_reservations_amount - retained_amount");
                table.HasCheckConstraint("ck_survey_cancellation_decisions_currency", "currency = 'VND'");
                table.HasCheckConstraint(
                    "ck_survey_cancellation_decisions_acceptable_capture",
                    "decision_kind <> 'CUSTOMER_AFTER_ACCEPTABLE_CAPTURE'::system.cancellation_decision_kind OR input_approval_evidence_id IS NOT NULL");
            });

        builder.HasKey(decision => decision.Id).HasName("pk_survey_cancellation_decisions");
        builder.HasAlternateKey(decision => new { decision.Id, decision.SurveyOrderId })
            .HasName("uq_survey_cancellation_decisions_id_order");

        builder.Property(decision => decision.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(decision => decision.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.OrderVersion).HasColumnName("order_version").HasColumnType("xid").IsRequired();
        builder.Property(decision => decision.RequesterId).HasColumnName("requester_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.DecisionActorId).HasColumnName("decision_actor_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.CancellationPolicyId).HasColumnName("cancellation_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.CancellationPolicyVersion).HasColumnName("cancellation_policy_version").HasColumnType("integer").IsRequired();
        builder.Property(decision => decision.DecisionKind).HasColumnName("decision_kind").HasColumnType("system.cancellation_decision_kind").IsRequired();
        builder.Property(decision => decision.RefundableBeforeCapture).HasColumnName("refundable_before_capture").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.RetainDepositAfterCapture).HasColumnName("retain_deposit_after_capture").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.AllowedFreeNoShowReschedules).HasColumnName("allowed_free_no_show_reschedules").HasColumnType("integer").IsRequired();
        builder.Property(decision => decision.OperationalPostponementRefundable).HasColumnName("operational_postponement_refundable").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.ServiceFailureRefundable).HasColumnName("service_failure_refundable").HasColumnType("boolean").IsRequired();
        builder.Property(decision => decision.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(decision => decision.ReasonDescription).HasColumnName("reason_description").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired();
        builder.Property(decision => decision.InputApprovalEvidenceId).HasColumnName("input_approval_evidence_id").HasColumnType("uuid");
        builder.Property(decision => decision.ConfirmedReceiptsAmount).HasColumnName("confirmed_receipts_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(decision => decision.CompletedRefundsAmount).HasColumnName("completed_refunds_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(decision => decision.ActiveRefundReservationsAmount).HasColumnName("active_refund_reservations_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(decision => decision.PinnedDepositAmount).HasColumnName("pinned_deposit_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(decision => decision.RetainedAmount).HasColumnName("retained_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(decision => decision.RefundAmount).HasColumnName("refund_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(decision => decision.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(decision => decision.OperationId).HasColumnName("operation_id").HasColumnType("uuid").IsRequired();
        builder.Property(decision => decision.PayloadFingerprint).HasColumnName("payload_fingerprint").HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(decision => decision.PredecessorDecisionId).HasColumnName("predecessor_decision_id").HasColumnType("uuid");
        builder.Property(decision => decision.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(decision => new { decision.SurveyOrderId, decision.OperationId })
            .HasDatabaseName("uq_survey_cancellation_decisions_order_operation")
            .IsUnique();
        builder.HasIndex(decision => decision.PredecessorDecisionId)
            .HasDatabaseName("uq_survey_cancellation_decisions_predecessor")
            .HasFilter("predecessor_decision_id IS NOT NULL")
            .IsUnique();

        builder.HasOne<SurveyOrder>()
            .WithMany()
            .HasForeignKey(decision => decision.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_cancellation_decisions_order");

        builder.HasOne<CancellationPolicy>()
            .WithMany()
            .HasForeignKey(decision => decision.CancellationPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_cancellation_decisions_policy");

        builder.HasOne<CaptureInputApproval>()
            .WithMany()
            .HasForeignKey(decision => decision.InputApprovalEvidenceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_cancellation_decisions_input_approval");

        builder.HasOne<SurveyCancellationDecision>()
            .WithMany()
            .HasForeignKey(decision => new
            {
                decision.PredecessorDecisionId,
                decision.SurveyOrderId
            })
            .HasPrincipalKey(predecessor => new
            {
                predecessor.Id,
                predecessor.SurveyOrderId
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_cancellation_decisions_predecessor_order");
    }
}
