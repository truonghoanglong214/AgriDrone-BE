using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class CancellationPolicyConfiguration : IEntityTypeConfiguration<CancellationPolicy>
{
    public void Configure(EntityTypeBuilder<CancellationPolicy> builder)
    {
        builder.ToTable(
            "cancellation_policies",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_cancellation_policies_window",
                    "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_cancellation_policies_no_shows",
                    "allowed_free_no_show_reschedules >= 0");
                table.HasCheckConstraint(
                    "ck_cancellation_policies_version",
                    "version_number >= 1");
            });

        builder.HasKey(policy => policy.Id).HasName("pk_cancellation_policies");
        builder.Property(policy => policy.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(policy => policy.VersionNumber).HasColumnName("version_number").HasColumnType("integer").IsRequired();
        builder.Property(policy => policy.PredecessorVersionId).HasColumnName("predecessor_version_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(policy => policy.RefundableBeforeCapture).HasColumnName("refundable_before_capture").HasColumnType("boolean").IsRequired();
        builder.Property(policy => policy.RetainDepositAfterCapture).HasColumnName("retain_deposit_after_capture").HasColumnType("boolean").IsRequired();
        builder.Property(policy => policy.AllowedFreeNoShowReschedules).HasColumnName("allowed_free_no_show_reschedules").HasColumnType("integer").IsRequired();
        builder.Property(policy => policy.OperationalPostponementRefundable).HasColumnName("operational_postponement_refundable").HasColumnType("boolean").IsRequired();
        builder.Property(policy => policy.ServiceFailureRefundable).HasColumnName("service_failure_refundable").HasColumnType("boolean").IsRequired();
        builder.Property(policy => policy.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(policy => policy.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(policy => policy.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(policy => policy.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasIndex(policy => policy.VersionNumber)
            .HasDatabaseName("uq_cancellation_policies_version")
            .IsUnique();

        builder.HasIndex(policy => policy.EffectiveFrom)
            .HasDatabaseName("ix_cancellation_policies_effective")
            .IsDescending(true);

        builder.HasOne<CancellationPolicy>()
            .WithMany()
            .HasForeignKey(policy => policy.PredecessorVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cancellation_policies_predecessor");
    }
}
