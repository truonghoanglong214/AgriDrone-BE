using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgriDrone.Modules.Surveys.Infrastructure.Persistence.Configurations;

public sealed class SurveyComboDiscountPolicyConfiguration
    : IEntityTypeConfiguration<SurveyComboDiscountPolicy>
{
    public void Configure(EntityTypeBuilder<SurveyComboDiscountPolicy> builder)
    {
        builder.ToTable("survey_combo_discount_policies", "survey", table =>
            table.HasCheckConstraint(
                "ck_survey_combo_discount_policies_percentage_valid",
                "discount_percentage >= 0 AND discount_percentage <= 100 AND (effective_to IS NOT NULL OR discount_percentage < 100)"));
        builder.HasKey(policy => policy.Id)
            .HasName("pk_survey_combo_discount_policies");
        builder.Property(policy => policy.Id)
            .HasColumnName("id").HasColumnType("uuid");
        builder.Property(policy => policy.VersionNumber)
            .HasColumnName("version_number").IsRequired();
        builder.Property(policy => policy.DiscountPercentage)
            .HasColumnName("discount_percentage")
            .HasColumnType("numeric(5,2)").IsRequired();
        builder.Property(policy => policy.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(policy => policy.EffectiveTo)
            .HasColumnName("effective_to")
            .HasColumnType("timestamp with time zone");
        builder.Property(policy => policy.CreatedBy)
            .HasColumnName("created_by").HasColumnType("uuid").IsRequired();
        builder.HasIndex(policy => policy.VersionNumber)
            .HasDatabaseName("uq_survey_combo_discount_policies_version").IsUnique();
        builder.HasIndex(policy => policy.EffectiveTo)
            .HasDatabaseName("uq_survey_combo_discount_policies_current")
            .HasFilter("effective_to IS NULL").IsUnique().AreNullsDistinct(false);
    }
}

