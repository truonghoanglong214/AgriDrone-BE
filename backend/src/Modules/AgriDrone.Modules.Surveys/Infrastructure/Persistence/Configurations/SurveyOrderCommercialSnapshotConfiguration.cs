using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyOrderCommercialSnapshotConfiguration
    : IEntityTypeConfiguration<SurveyOrderCommercialSnapshot>
{
    public void Configure(EntityTypeBuilder<SurveyOrderCommercialSnapshot> builder)
    {
        builder.ToTable(
            "order_commercial_snapshots",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_order_commercial_snapshots_price", "price_per_pole > 0 AND price_per_pole = FLOOR(price_per_pole)");
                table.HasCheckConstraint("ck_order_commercial_snapshots_deposit", "deposit_amount > 0 AND deposit_amount = FLOOR(deposit_amount)");
                table.HasCheckConstraint("ck_order_commercial_snapshots_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_order_commercial_snapshots_claim_window", "claim_window_days >= 1");
                table.HasCheckConstraint("ck_order_commercial_snapshots_reschedules", "allowed_free_no_show_reschedules >= 0");
            });

        builder.HasKey(s => s.SurveyOrderId).HasName("pk_order_commercial_snapshots");
        builder.Property(s => s.SurveyOrderId).HasColumnName("survey_order_id").HasColumnType("uuid");
        builder.Property(s => s.BusinessPolicyVersionId).HasColumnName("business_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.PolicyFingerprint).HasColumnName("policy_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(s => s.SurveyServiceRevisionId).HasColumnName("survey_service_revision_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.SurveyServiceRevisionVersion).HasColumnName("survey_service_revision_version").HasColumnType("integer").IsRequired();
        builder.Property(s => s.ServiceCode).HasColumnName("service_code").HasColumnType("character varying(32)").HasMaxLength(32).IsRequired();
        builder.Property(s => s.MapsPoles).HasColumnName("maps_poles").HasColumnType("boolean").IsRequired();
        builder.Property(s => s.ScreensDisease).HasColumnName("screens_disease").HasColumnType("boolean").IsRequired();
        builder.Property(s => s.PriceVersionId).HasColumnName("price_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.PriceVersionNumber).HasColumnName("price_version_number").HasColumnType("integer").IsRequired();
        builder.Property(s => s.PricePerPole).HasColumnName("price_per_pole").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(s => s.DepositPolicyId).HasColumnName("deposit_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.DepositVersionNumber).HasColumnName("deposit_version_number").HasColumnType("integer").IsRequired();
        builder.Property(s => s.DepositAmount).HasColumnName("deposit_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(s => s.ClaimPolicyId).HasColumnName("claim_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.ClaimPolicyVersionNumber).HasColumnName("claim_policy_version_number").HasColumnType("integer").IsRequired();
        builder.Property(s => s.ClaimWindowDays).HasColumnName("claim_window_days").HasColumnType("integer").IsRequired();
        builder.Property(s => s.ClaimTimeZoneId).HasColumnName("claim_time_zone_id").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(s => s.ExcludeNoticeDate).HasColumnName("exclude_notice_date").HasColumnType("boolean").IsRequired();
        builder.Property(s => s.CancellationPolicyId).HasColumnName("cancellation_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(s => s.CancellationPolicyVersionNumber).HasColumnName("cancellation_policy_version_number").HasColumnType("integer").IsRequired();
        builder.Property(s => s.RefundableBeforeCapture).HasColumnName("refundable_before_capture").HasColumnType("boolean").IsRequired();
        builder.Property(s => s.RetainDepositAfterCapture).HasColumnName("retain_deposit_after_capture").HasColumnType("boolean").IsRequired();
        builder.Property(s => s.AllowedFreeNoShowReschedules).HasColumnName("allowed_free_no_show_reschedules").HasColumnType("integer").IsRequired();
        builder.Property(s => s.EvaluatedAt).HasColumnName("evaluated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasOne<SurveyOrder>()
            .WithOne()
            .HasForeignKey<SurveyOrderCommercialSnapshot>(s => s.SurveyOrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_order");

        builder.HasOne<BusinessPolicyVersion>()
            .WithMany()
            .HasForeignKey(s => s.BusinessPolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_policy_version");

        builder.HasOne<SurveyServiceRevision>()
            .WithMany()
            .HasForeignKey(s => s.SurveyServiceRevisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_service_revision");

        builder.HasOne<SurveyServicePrice>()
            .WithMany()
            .HasForeignKey(s => s.PriceVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_price_version");

        builder.HasOne<SurveyDepositPolicy>()
            .WithMany()
            .HasForeignKey(s => s.DepositPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_deposit_policy");

        builder.HasOne<ClaimPolicy>()
            .WithMany()
            .HasForeignKey(s => s.ClaimPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_claim_policy");

        builder.HasOne<CancellationPolicy>()
            .WithMany()
            .HasForeignKey(s => s.CancellationPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_commercial_snapshots_cancellation_policy");
    }
}
