using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyDepositPolicyConfiguration
    : IEntityTypeConfiguration<SurveyDepositPolicy>
{
    public void Configure(EntityTypeBuilder<SurveyDepositPolicy> builder)
    {
        builder.ToTable("survey_deposit_policies", "survey", table =>
            table.HasCheckConstraint(
                "ck_survey_deposit_policies_amount_positive",
                "amount_vnd > 0 AND amount_vnd = trunc(amount_vnd)"));
        builder.HasKey(policy => policy.Id)
            .HasName("pk_survey_deposit_policies");
        builder.Property(policy => policy.Id)
            .HasColumnName("id").HasColumnType("uuid");
        builder.Property(policy => policy.VersionNumber)
            .HasColumnName("version_number").IsRequired();
        builder.Property(policy => policy.AmountVnd)
            .HasColumnName("amount_vnd")
            .HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(policy => policy.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.EffectiveTo)
            .HasColumnName("effective_to")
            .HasColumnType("timestamp with time zone");
        builder.Property(policy => policy.CreatedBy)
            .HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.HasIndex(policy => policy.VersionNumber)
            .HasDatabaseName("uq_survey_deposit_policies_version").IsUnique();
        builder.HasIndex(policy => policy.EffectiveTo)
            .HasDatabaseName("uq_survey_deposit_policies_current")
            .HasFilter("effective_to IS NULL").IsUnique();
    }
}
