using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyDepositPolicyConfiguration : IEntityTypeConfiguration<SurveyDepositPolicy>
{
    public void Configure(EntityTypeBuilder<SurveyDepositPolicy> builder)
    {
        builder.ToTable(
            "v3_survey_deposit_policies",
            "survey",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_survey_deposit_policies_window",
                    "effective_to IS NULL OR effective_to > effective_from");
                table.HasCheckConstraint(
                    "ck_survey_deposit_policies_amount",
                    "amount > 0 AND amount = FLOOR(amount)");
                table.HasCheckConstraint(
                    "ck_survey_deposit_policies_currency",
                    "currency ~ '^[A-Z]{3}$'");
                table.HasCheckConstraint(
                    "ck_survey_deposit_policies_version",
                    "version_number >= 1");
            });

        builder.HasKey(policy => policy.Id).HasName("pk_v3_survey_deposit_policies");
        builder.Property(policy => policy.Id).HasColumnName("id").HasColumnType("uuid");
        builder.Property(policy => policy.VersionNumber).HasColumnName("version_number").HasColumnType("integer").IsRequired();
        builder.Property(policy => policy.PredecessorVersionId).HasColumnName("predecessor_version_id").HasColumnType("uuid").IsRequired(false);
        builder.Property(policy => policy.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasPrecision(18, 2).IsRequired();
        builder.Property(policy => policy.Currency).HasColumnName("currency").HasColumnType("character(3)").HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(policy => policy.EffectiveFrom).HasColumnName("effective_from").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.EffectiveTo).HasColumnName("effective_to").HasColumnType("timestamp with time zone").IsRequired(false);
        builder.Property(policy => policy.CreatedBy).HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.Property(policy => policy.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.ReasonCode).HasColumnName("reason_code").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(policy => policy.EvidenceReference).HasColumnName("evidence_reference").HasColumnType("character varying(500)").HasMaxLength(500).IsRequired(false);

        builder.HasIndex(policy => policy.VersionNumber)
            .HasDatabaseName("uq_v3_survey_deposit_policies_version")
            .IsUnique();

        builder.HasIndex(policy => policy.EffectiveFrom)
            .HasDatabaseName("ix_v3_survey_deposit_policies_effective")
            .IsDescending(true);

        builder.HasOne<SurveyDepositPolicy>()
            .WithMany()
            .HasForeignKey(policy => policy.PredecessorVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_survey_deposit_policies_predecessor");
    }
}
