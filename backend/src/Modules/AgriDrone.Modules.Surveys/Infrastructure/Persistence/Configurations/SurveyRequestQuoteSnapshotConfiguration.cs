using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyRequestQuoteSnapshotConfiguration : IEntityTypeConfiguration<SurveyRequestQuoteSnapshot>
{
    public void Configure(EntityTypeBuilder<SurveyRequestQuoteSnapshot> builder)
    {
        builder.ToTable(
            "request_quote_snapshots",
            "survey",
            table =>
            {
                table.HasCheckConstraint("ck_request_quote_snapshots_price", "price_per_pole > 0 AND price_per_pole = FLOOR(price_per_pole)");
                table.HasCheckConstraint("ck_request_quote_snapshots_deposit", "deposit_amount > 0 AND deposit_amount = FLOOR(deposit_amount)");
                table.HasCheckConstraint("ck_request_quote_snapshots_currency", "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint("ck_request_quote_snapshots_poles", "estimated_pole_count IS NULL OR estimated_pole_count > 0");
            });

        builder.HasKey(q => q.Id).HasName("pk_request_quote_snapshots");
        builder.Property(q => q.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(q => q.SurveyRequestId).HasColumnName("survey_request_id").HasColumnType("uuid").IsRequired();
        builder.Property(q => q.BusinessPolicyVersionId).HasColumnName("business_policy_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(q => q.PolicyFingerprint).HasColumnName("policy_fingerprint").HasColumnType("character varying(64)").HasMaxLength(64).IsRequired();
        builder.Property(q => q.SurveyServiceRevisionId).HasColumnName("survey_service_revision_id").HasColumnType("uuid").IsRequired();
        builder.Property(q => q.SurveyServiceRevisionVersion).HasColumnName("survey_service_revision_version").HasColumnType("integer").IsRequired();
        builder.Property(q => q.PriceVersionId).HasColumnName("price_version_id").HasColumnType("uuid").IsRequired();
        builder.Property(q => q.PriceVersionNumber).HasColumnName("price_version_number").HasColumnType("integer").IsRequired();
        builder.Property(q => q.PricePerPole).HasColumnName("price_per_pole").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(q => q.DepositPolicyId).HasColumnName("deposit_policy_id").HasColumnType("uuid").IsRequired();
        builder.Property(q => q.DepositVersionNumber).HasColumnName("deposit_version_number").HasColumnType("integer").IsRequired();
        builder.Property(q => q.DepositAmount).HasColumnName("deposit_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(q => q.EstimatedPoleCount).HasColumnName("estimated_pole_count").HasColumnType("integer").IsRequired(false);
        builder.Property(q => q.IndicativeTotalAmount).HasColumnName("indicative_total_amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired(false);
        builder.Property(q => q.EvaluatedAt).HasColumnName("evaluated_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(q => q.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(q => q.SurveyRequestId)
            .HasDatabaseName("uq_request_quote_snapshots_request")
            .IsUnique();

        builder.HasOne<SurveyRequest>()
            .WithMany()
            .HasForeignKey(q => q.SurveyRequestId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_request_quote_snapshots_request");

        builder.HasOne<BusinessPolicyVersion>()
            .WithMany()
            .HasForeignKey(q => q.BusinessPolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_request_quote_snapshots_policy_version");

        builder.HasOne<SurveyServiceRevision>()
            .WithMany()
            .HasForeignKey(q => q.SurveyServiceRevisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_request_quote_snapshots_service_revision");

        builder.HasOne<SurveyServicePrice>()
            .WithMany()
            .HasForeignKey(q => q.PriceVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_request_quote_snapshots_price_version");

        builder.HasOne<SurveyDepositPolicy>()
            .WithMany()
            .HasForeignKey(q => q.DepositPolicyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_request_quote_snapshots_deposit_policy");
    }
}
